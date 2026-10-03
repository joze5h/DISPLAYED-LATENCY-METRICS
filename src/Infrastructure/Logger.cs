using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace DisplayedLatencyMetrics.Infrastructure;

internal static class Logger
{
    private const long MaximumLogSizeBytes = 2L * 1024 * 1024;

    private static readonly Lock Sync = new();

    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DISPLAYED-LATENCY-METRICS",
        "LOG");

    private static readonly string LogPath = Path.Combine(LogDirectory, "LOG.log");
    private static readonly string PreviousLogPath = Path.Combine(LogDirectory, "LOG.previous.log");
    private static int _initialized;

    internal static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    internal static void Warning(
        string message,
        Exception? exception = null,
        [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string member = "") =>
        WriteBug("Warning: " + message, exception, sourceFile, sourceLine, member);

    internal static void Error(
        string message,
        Exception? exception = null,
        [CallerFilePath] string sourceFile = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerMemberName] string member = "") =>
        WriteBug(message, exception, sourceFile, sourceLine, member);

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        WriteBug(
            $"Unhandled process exception. Terminating={args.IsTerminating}",
            args.ExceptionObject as Exception,
            "<runtime>",
            0,
            nameof(OnUnhandledException));
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        WriteBug("Unobserved task exception", args.Exception, "<runtime>", 0, nameof(OnUnobservedTaskException));
        args.SetObserved();
    }

    private static void WriteBug(
        string message,
        Exception? exception,
        string sourceFile,
        int sourceLine,
        string member)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeededLocked();

                var entry = new StringBuilder(512);
                entry.Append(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                entry.AppendLine(" [BUG]");
                entry.Append("THREAD: ").Append(Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture));
                entry.AppendLine();
                entry.Append("WHERE: ").Append(Path.GetFileName(sourceFile));
                if (sourceLine > 0)
                {
                    entry.Append(':').Append(sourceLine.ToString(CultureInfo.InvariantCulture));
                }
                if (!string.IsNullOrWhiteSpace(member))
                {
                    entry.Append(" :: ").Append(member);
                }
                entry.AppendLine();
                entry.Append("WHAT: ").AppendLine(message.ReplaceLineEndings(" "));
                if (exception is not null)
                {
                    entry.Append("WHY: ").Append(exception.GetType().FullName).Append(": ");
                    entry.AppendLine(exception.Message.ReplaceLineEndings(" "));
                    entry.AppendLine("DETAILS:");
                    entry.AppendLine(exception.ToString());
                }
                entry.AppendLine();

                using var stream = new FileStream(
                    LogPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 4096,
                    FileOptions.WriteThrough);
                using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write(entry.ToString());
                writer.Flush();
            }
        }
        catch
        {

        }
    }

    private static void RotateIfNeededLocked()
    {
        if (!File.Exists(LogPath) || new FileInfo(LogPath).Length < MaximumLogSizeBytes)
        {
            return;
        }

        File.Move(LogPath, PreviousLogPath, overwrite: true);
    }
}

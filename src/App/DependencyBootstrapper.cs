using DisplayedLatencyMetrics.Infrastructure;
using DisplayedLatencyMetrics.Interop;
using DisplayedLatencyMetrics.Overlay;
using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WebView2.Utilities;

namespace DisplayedLatencyMetrics.App;

internal static unsafe class DependencyBootstrapper
{
    private const string PresentMonUrl = "https://dwtbd.hb.ru-msk.vkcloud-storage.ru/PresentMon/PresentMon.msi";
    private const string WebView2Url = "https://dwtbd.hb.ru-msk.vkcloud-storage.ru/PresentMon/WebView2Runtime.exe";
    private const string RtssUrl = "https://dwtbd.hb.ru-msk.vkcloud-storage.ru/PresentMon/RTSSSetup.exe";
    private static readonly Guid WinTrustActionGenericVerifyV2 = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

    internal static bool EnsureDependencies()
    {
        if (PresentMonRuntime.FindApiDll() is not null && IsWebView2Available() && RtssOverlayPublisher.IsAvailable())
        {
            return true;
        }

        using var window = new DependencyStatusWindow();
        return window.Run();
    }

    internal static bool RepairWebView2()
    {
        using var window = new DependencyStatusWindow(forceWebView2Repair: true);
        return window.Run();
    }

    private static bool IsWebView2Available()
    {
        try
        {
            WebView2Utilities.Initialize(Assembly.GetEntryAssembly());
            return !string.IsNullOrWhiteSpace(
                WebView2Utilities.GetAvailableCoreWebView2BrowserVersionString());
        }
        catch
        {

            return false;
        }
    }

    private sealed unsafe class DependencyStatusWindow : IDisposable
    {
        private const string WindowClassName = "DisplayedLatencyMetrics.NativeAot.DependencyBootstrap";
        private const uint WmProgress = Win32.WM_APP + 91;
        private const uint WmFinished = Win32.WM_APP + 92;

        private static DependencyStatusWindow? s_current;

        private readonly Lock _stateSync = new();
        private readonly bool _forceWebView2Repair;
        private nint _instance;
        private nint _window;
        private nint _className;
        private UiTheme? _theme;
        private nint _presentState;
        private nint _presentBar;
        private nint _webViewState;
        private nint _webViewBar;
        private nint _rtssState;
        private nint _rtssBar;
        private nint _detail;
        private int _presentPercent;
        private int _webViewPercent;
        private int _rtssPercent;
        private string _presentText = "ПРОВЕРКА";
        private string _webViewText = "ПРОВЕРКА";
        private string _rtssText = "ПРОВЕРКА";
        private string _detailText = "Проверка установленных компонентов…";
        private bool _success;
        private bool _finished;
        private bool _disposed;

        internal DependencyStatusWindow(bool forceWebView2Repair = false)
        {
            _forceWebView2Repair = forceWebView2Repair;
        }

        internal bool Run()
        {
            s_current = this;
            _instance = Win32.GetModuleHandle(null);
            _theme = new UiTheme();
            _className = Marshal.StringToHGlobalUni(WindowClassName);

            Win32.WindowClassEx windowClass = new()
            {
                Size = (uint)sizeof(Win32.WindowClassEx),
                Instance = _instance,
                WindowProcedure = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>)&WindowProcedure,
                BackgroundBrush = _theme.BackgroundBrush,
                ClassName = _className,
            };
            if (Win32.RegisterClassEx(ref windowClass) == 0)
            {
                Logger.Error($"Unable to register dependency status window. Win32={Marshal.GetLastWin32Error()}");
                return false;
            }

            int windowWidth = _theme.Scale(620);
            int windowHeight = _theme.Scale(382);
            int x = Math.Max(0, (Win32.GetSystemMetrics(Win32.SM_CXSCREEN) - windowWidth) / 2);
            int y = Math.Max(0, (Win32.GetSystemMetrics(Win32.SM_CYSCREEN) - windowHeight) / 2);

            _window = Win32.CreateWindowEx(
                Win32.WS_EX_APPWINDOW,
                WindowClassName,
                "DISPLAYED-LATENCY-METRICS — УСТАНОВКА КОМПОНЕНТОВ",
                Win32.WS_CAPTION | Win32.WS_SYSMENU | Win32.WS_CLIPCHILDREN,
                x,
                y,
                windowWidth,
                windowHeight,
                0,
                0,
                _instance,
                0);
            if (_window == 0)
            {
                Logger.Error($"Unable to create dependency status window. Win32={Marshal.GetLastWin32Error()}");
                return false;
            }

            UiTheme.ApplyTopLevelStyle(_window);
            Win32.ShowWindow(_window, Win32.SW_SHOW);
            Win32.UpdateWindow(_window);

            var worker = new Thread(InstallWorker)
            {
                IsBackground = true,
                Name = "Dependency bootstrap",
                Priority = ThreadPriority.BelowNormal,
            };
            worker.Start();

            while (true)
            {
                int result = Win32.GetMessage(out Win32.Message message, 0, 0, 0);
                if (result <= 0)
                {
                    break;
                }
                Win32.TranslateMessage(ref message);
                Win32.DispatchMessage(ref message);
            }
            return _success;
        }

        private void CreateControls(nint window)
        {
            int margin = _theme!.Scale(24);
            int width = _theme.Scale(556);
            int titleHeight = _theme.Scale(24);
            int labelHeight = _theme.Scale(20);
            int barHeight = _theme.Scale(22);

            CreateLabel(window, "УСТАНОВКА КОМПОНЕНТОВ", margin, _theme.Scale(20), width, titleHeight, _theme.UiSemiboldFont);
            CreateLabel(window, "Проверка, загрузка, установка и запуск обязательных зависимостей", margin, _theme.Scale(48), width, labelHeight, _theme.UiSmallFont);

            CreateLabel(window, "PRESENTMON API", margin, _theme.Scale(90), _theme.Scale(330), labelHeight, _theme.UiSemiboldFont);
            _presentState = CreateLabel(window, _presentText, _theme.Scale(410), _theme.Scale(90), _theme.Scale(146), labelHeight, _theme.UiSmallFont);
            _presentBar = CreateLabel(window, BuildBar(0), margin, _theme.Scale(114), width, barHeight, _theme.UiSmallFont);

            CreateLabel(window, "MICROSOFT EDGE WEBVIEW2 RUNTIME", margin, _theme.Scale(156), _theme.Scale(330), labelHeight, _theme.UiSemiboldFont);
            _webViewState = CreateLabel(window, _webViewText, _theme.Scale(410), _theme.Scale(156), _theme.Scale(146), labelHeight, _theme.UiSmallFont);
            _webViewBar = CreateLabel(window, BuildBar(0), margin, _theme.Scale(180), width, barHeight, _theme.UiSmallFont);

            CreateLabel(window, "RIVATUNER STATISTICS SERVER (RTSS)", margin, _theme.Scale(222), _theme.Scale(330), labelHeight, _theme.UiSemiboldFont);
            _rtssState = CreateLabel(window, _rtssText, _theme.Scale(410), _theme.Scale(222), _theme.Scale(146), labelHeight, _theme.UiSmallFont);
            _rtssBar = CreateLabel(window, BuildBar(0), margin, _theme.Scale(246), width, barHeight, _theme.UiSmallFont);

            _detail = CreateLabel(window, _detailText, margin, _theme.Scale(292), width, _theme.Scale(48), _theme.UiSmallFont);
        }

        private nint CreateLabel(nint parent, string text, int x, int y, int width, int height, nint font)
        {
            nint control = Win32.CreateWindowEx(
                0,
                "STATIC",
                text,
                Win32.WS_CHILD | Win32.WS_VISIBLE,
                x,
                y,
                width,
                height,
                parent,
                0,
                _instance,
                0);
            UiTheme.ApplyControl(control, font);
            return control;
        }

        private void InstallWorker()
        {
            try
            {
                bool presentMonReady = PresentMonRuntime.FindApiDll() is not null;
                SetPresent(presentMonReady ? 100 : 0, presentMonReady ? "ГОТОВО" : "НЕТ", presentMonReady
                    ? "PresentMon API уже установлен."
                    : "PresentMon API не найден: начинается загрузка.");
                if (!presentMonReady && !DownloadInstallAndVerify(
                        "PresentMon API",
                        PresentMonUrl,
                        "PresentMon.msi",
                        (installer, log) => $"/i \"{installer}\" /norestart /L*v \"{log}\"",
                        true,
                        false,
                        () => PresentMonRuntime.FindApiDll() is not null,
                        SetPresent))
                {
                    Finish(false);
                    return;
                }

                bool webViewReady = !_forceWebView2Repair && IsWebView2Available();
                SetWebView(webViewReady ? 100 : 0, webViewReady ? "ГОТОВО" : "НЕТ", webViewReady
                    ? "WebView2 Runtime уже установлен."
                    : "WebView2 Runtime не найден: начинается загрузка.");
                if (!webViewReady && !DownloadInstallAndVerify(
                        "WebView2 Runtime",
                        WebView2Url,
                        "WebView2Runtime.exe",

                        (_, _) => string.Empty,
                        false,
                        true,
                        IsWebView2Available,
                        SetWebView))
                {
                    Finish(false);
                    return;
                }

                bool rtssReady = RtssOverlayPublisher.IsAvailable();
                string? rtssExecutable = FindRtssExecutable();
                bool rtssInstalled = rtssReady || rtssExecutable is not null;
                SetRtss(rtssReady ? 100 : rtssInstalled ? 65 : 0, rtssReady ? "ГОТОВО" : rtssInstalled ? "НЕ ЗАПУЩЕН" : "НЕТ", rtssReady
                    ? "RTSS установлен, запущен и канал OSD доступен."
                    : rtssInstalled
                        ? "RTSS установлен, но не запущен: выполняется запуск."
                        : "RTSS не найден: начинается загрузка установщика.");

                if (!rtssInstalled && !DownloadInstallAndVerify(
                        "RivaTuner Statistics Server",
                        RtssUrl,
                        "RTSSSetup.exe",
                        (_, _) => string.Empty,
                        true,
                        false,
                        () => FindRtssExecutable() is not null || RtssOverlayPublisher.IsAvailable(),
                        SetRtss))
                {
                    Finish(false);
                    return;
                }

                if (!EnsureRtssRunning(SetRtss))
                {
                    Finish(false);
                    return;
                }

                SetDetail("Компоненты готовы. Запуск DISPLAYED-LATENCY-METRICS…");
                Finish(true);
            }
            catch (Exception exception)
            {
                Logger.Error("Dependency bootstrapper failed", exception);
                SetDetail("Ошибка подготовки компонентов. Подробности: LOG.log");
                Win32.MessageBox(_window, exception.Message, "DISPLAYED-LATENCY-METRICS", Win32.MB_OK | Win32.MB_ICONERROR);
                Finish(false);
            }
        }

        private static bool DownloadInstallAndVerify(
            string name,
            string url,
            string fileName,
            Func<string, string, string> arguments,
            bool interactiveInstaller,
            bool waitForEdgeUpdater,
            Func<bool> verify,
            Action<int, string, string> update)
        {
            try
            {
                string runtimeDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DISPLAYED-LATENCY-METRICS",
                    "Runtime");
                Directory.CreateDirectory(runtimeDirectory);
                string installer = Path.Combine(runtimeDirectory, fileName);
                string partial = installer + ".download";
                string installLog = Path.Combine(runtimeDirectory, fileName + ".install.log");

                bool useCachedInstaller = File.Exists(installer) && HasTrustedSignature(installer);
                if (useCachedInstaller)
                {
                    update(100, "ФАЙЛ НАЙДЕН", $"Используется сохранённый {fileName}.");
                }
                else
                {
                    update(0, "ЗАГРУЗКА", $"Загрузка {name}…");
                    Download(url, partial, percent => update(percent, "ЗАГРУЗКА", $"Загрузка {name}: {percent}%"));
                    if (!HasTrustedSignature(partial))
                    {
                        throw new InvalidDataException($"{name} installer does not have a trusted Authenticode signature.");
                    }
                    File.Move(partial, installer, overwrite: true);
                }

                update(
                    100,
                    interactiveInstaller ? "ОЖИДАНИЕ" : "УСТАНОВКА",
                    interactiveInstaller
                        ? $"Открыт мастер {name}. Завершите установку в его окне — программа продолжит автоматически."
                        : $"Установка {name}. Windows может запросить подтверждение UAC.");
                using Process? process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ? "msiexec.exe" : installer,
                    Arguments = arguments(installer, installLog),
                    UseShellExecute = true,
                    Verb = string.Empty,
                    WindowStyle = ProcessWindowStyle.Normal,
                });
                if (process is null)
                {
                    throw new InvalidOperationException($"Unable to start {name} installer.");
                }
                process.WaitForExit();
                if (process.ExitCode != 0 && process.ExitCode != 3010)
                {
                    throw new InvalidOperationException(
                        $"{name} installer exited with code {process.ExitCode}. Installer log: {installLog}");
                }

                if (waitForEdgeUpdater && !WaitForEdgeUpdater(update))
                {
                    throw new TimeoutException(
                        "MicrosoftEdgeUpdate.exe did not finish the WebView2 Runtime installation within the allowed time.");
                }

                if (!WaitForComponent(verify))
                {
                    throw new InvalidOperationException($"{name} installation completed, but the component is still unavailable.");
                }

                update(100, "ГОТОВО", $"{name} готов к работе.");
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error($"Unable to download or install {name}", exception);
                update(0, "ОШИБКА", $"Не удалось подготовить {name}. Подробности: LOG.log");
                return false;
            }
        }

        private static bool EnsureRtssRunning(Action<int, string, string> update)
        {
            if (RtssOverlayPublisher.IsAvailable())
            {
                update(100, "ГОТОВО", "RTSS установлен, запущен и канал OSD доступен.");
                return true;
            }

            string? executable = FindRtssExecutable();
            if (executable is null)
            {
                update(0, "ОШИБКА", "RTSS не найден после установки. Подробности: LOG.log");
                Logger.Error("RTSS executable was not found after installation.");
                return false;
            }

            try
            {
                if (CountRunningProcesses("RTSS") == 0)
                {
                    update(75, "ЗАПУСК", "Запуск RivaTuner Statistics Server для режима «На весь экран»…");
                    using Process? process = Process.Start(new ProcessStartInfo
                    {
                        FileName = executable,
                        WorkingDirectory = Path.GetDirectoryName(executable) ?? string.Empty,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal,
                    });
                    if (process is null)
                    {
                        throw new InvalidOperationException("Unable to start RTSS.exe.");
                    }
                }
                else
                {
                    update(80, "ПРОВЕРКА", "RTSS уже запущен. Ожидание готовности канала OSD…");
                }

                const int attempts = 120;
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    if (RtssOverlayPublisher.IsAvailable())
                    {
                        update(100, "ГОТОВО", "RTSS установлен, запущен и канал OSD доступен.");
                        return true;
                    }

                    int percent = 80 + Math.Min(19, attempt * 20 / attempts);
                    update(percent, "ЗАПУСК", "Ожидание запуска RTSS и инициализации OSD…");
                    Thread.Sleep(250);
                }

                update(0, "ОШИБКА", "RTSS запущен, но канал OSD недоступен. Проверьте RTSS и LOG.log.");
                Logger.Error("RTSS process did not expose RTSSSharedMemoryV2 within 30 seconds.");
                return false;
            }
            catch (Exception exception)
            {
                Logger.Error("Unable to start RTSS", exception);
                update(0, "ОШИБКА", "Не удалось запустить RTSS. Подробности: LOG.log");
                return false;
            }
        }

        private static string? FindRtssExecutable()
        {
            foreach (string candidate in RtssExecutableCandidates())
            {
                try
                {
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
                catch
                {

                }
            }

            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName("RTSS");
            }
            catch
            {
                return null;
            }

            try
            {
                foreach (Process process in processes)
                {
                    try
                    {
                        string? path = process.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        {
                            return path;
                        }
                    }
                    catch
                    {

                    }
                }
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }

            return null;
        }

        private static List<string> RtssExecutableCandidates()
        {
            var candidates = new List<string>();

            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                candidates.Add(Path.Combine(programFilesX86, "RivaTuner Statistics Server", "RTSS.exe"));
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                candidates.Add(Path.Combine(programFiles, "RivaTuner Statistics Server", "RTSS.exe"));
            }

            foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            {
                foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                {
                    try
                    {
                        using RegistryKey? baseKey = TryOpenRegistryBaseKey(hive, view);
                        using RegistryKey? uninstall = baseKey?.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                        if (uninstall is null)
                        {
                            continue;
                        }

                        foreach (string subKeyName in uninstall.GetSubKeyNames())
                        {
                            try
                            {
                                using RegistryKey? product = uninstall.OpenSubKey(subKeyName);
                                string displayName = product?.GetValue("DisplayName") as string ?? string.Empty;
                                if (!displayName.Contains("RivaTuner Statistics Server", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                string installLocation = product?.GetValue("InstallLocation") as string ?? string.Empty;
                                if (!string.IsNullOrWhiteSpace(installLocation))
                                {
                                    candidates.Add(Path.Combine(installLocation.Trim().Trim('"'), "RTSS.exe"));
                                }

                                string displayIcon = product?.GetValue("DisplayIcon") as string ?? string.Empty;
                                string? iconPath = ExtractExecutablePath(displayIcon);
                                if (!string.IsNullOrWhiteSpace(iconPath))
                                {
                                    string? iconDirectory = Path.GetDirectoryName(iconPath);
                                    if (!string.IsNullOrWhiteSpace(iconDirectory))
                                    {
                                        candidates.Add(Path.Combine(iconDirectory, "RTSS.exe"));
                                    }
                                }
                            }
                            catch
                            {

                            }
                        }
                    }
                    catch
                    {

                    }
                }
            }

            return candidates;
        }

        private static RegistryKey? TryOpenRegistryBaseKey(RegistryHive hive, RegistryView view)
        {
            try
            {
                return RegistryKey.OpenBaseKey(hive, view);
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractExecutablePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();
            if (trimmed.StartsWith('"'))
            {
                int closingQuote = trimmed.IndexOf('"', 1);
                return closingQuote > 1 ? trimmed[1..closingQuote] : trimmed.Trim('"');
            }

            int comma = trimmed.IndexOf(',');
            return comma > 0 ? trimmed[..comma].Trim() : trimmed;
        }

        private static bool WaitForEdgeUpdater(Action<int, string, string> update)
        {

            TimeSpan startGracePeriod = TimeSpan.FromSeconds(5);
            TimeSpan stableExitPeriod = TimeSpan.FromSeconds(2);
            TimeSpan timeout = TimeSpan.FromMinutes(10);
            var clock = Stopwatch.StartNew();
            TimeSpan? noProcessesSince = null;
            bool observedUpdater = false;

            while (clock.Elapsed < timeout)
            {
                int processCount = CountRunningProcesses("MicrosoftEdgeUpdate");
                if (processCount > 0)
                {
                    observedUpdater = true;
                    noProcessesSince = null;
                    update(
                        100,
                        "УСТАНОВКА",
                        $"WebView2 Runtime устанавливается: MicrosoftEdgeUpdate.exe ({processCount}).");
                }
                else if (observedUpdater)
                {
                    noProcessesSince ??= clock.Elapsed;
                    if (clock.Elapsed - noProcessesSince.Value >= stableExitPeriod)
                    {
                        return true;
                    }
                }
                else if (clock.Elapsed >= startGracePeriod)
                {

                    return true;
                }

                Thread.Sleep(250);
            }

            return false;
        }

        private static int CountRunningProcesses(string processName)
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch
            {

                return 0;
            }

            try
            {
                int running = 0;
                foreach (Process process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            running++;
                        }
                    }
                    catch
                    {

                    }
                }
                return running;
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }

        private static bool WaitForComponent(Func<bool> verify)
        {

            const int retryCount = 30;
            for (int attempt = 0; attempt < retryCount; attempt++)
            {
                if (verify())
                {
                    return true;
                }
                Thread.Sleep(1000);
            }
            return verify();
        }

        private static void Download(string url, string destination, Action<int> progress)
        {
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            using HttpResponseMessage response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? -1;
            using Stream source = response.Content.ReadAsStream();
            using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 131_072, FileOptions.WriteThrough);
            byte[] buffer = GC.AllocateUninitializedArray<byte>(131_072);
            long received = 0;
            int lastPercent = -1;
            while (true)
            {
                int count = source.Read(buffer, 0, buffer.Length);
                if (count == 0)
                {
                    break;
                }
                target.Write(buffer, 0, count);
                received += count;
                if (total > 0)
                {
                    int percent = Math.Clamp((int)(received * 100 / total), 0, 100);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress(percent);
                    }
                }
            }
            target.Flush(flushToDisk: true);
            progress(100);
        }

        private static bool HasTrustedSignature(string file)
        {
            nint filePath = Marshal.StringToHGlobalUni(file);
            try
            {
                Win32.WinTrustFileInfo fileInfo = new()
                {
                    Size = (uint)sizeof(Win32.WinTrustFileInfo),
                    FilePath = filePath,
                };
                Win32.WinTrustData trustData = new()
                {
                    Size = (uint)sizeof(Win32.WinTrustData),
                    UiChoice = 2,
                    RevocationChecks = 0,
                    UnionChoice = 1,
                    FileInfo = (nint)(&fileInfo),
                    StateAction = 0,
                };
                Guid action = WinTrustActionGenericVerifyV2;
                return Win32.WinVerifyTrust(0, ref action, ref trustData) == 0;
            }
            finally
            {
                Marshal.FreeHGlobal(filePath);
            }
        }

        private void SetPresent(int percent, string state, string detail)
        {
            lock (_stateSync)
            {
                _presentPercent = percent;
                _presentText = state;
                _detailText = detail;
            }
            NotifyProgress();
        }

        private void SetWebView(int percent, string state, string detail)
        {
            lock (_stateSync)
            {
                _webViewPercent = percent;
                _webViewText = state;
                _detailText = detail;
            }
            NotifyProgress();
        }

        private void SetRtss(int percent, string state, string detail)
        {
            lock (_stateSync)
            {
                _rtssPercent = percent;
                _rtssText = state;
                _detailText = detail;
            }
            NotifyProgress();
        }

        private void SetDetail(string detail)
        {
            lock (_stateSync)
            {
                _detailText = detail;
            }
            NotifyProgress();
        }

        private void NotifyProgress()
        {
            if (_window != 0)
            {
                Win32.PostMessage(_window, WmProgress, 0, 0);
            }
        }

        private void RenderProgress()
        {
            int presentPercent;
            int webViewPercent;
            int rtssPercent;
            string presentText;
            string webViewText;
            string rtssText;
            string detail;
            lock (_stateSync)
            {
                presentPercent = _presentPercent;
                webViewPercent = _webViewPercent;
                rtssPercent = _rtssPercent;
                presentText = _presentText;
                webViewText = _webViewText;
                rtssText = _rtssText;
                detail = _detailText;
            }
            Win32.SetWindowText(_presentState, presentText);
            Win32.SetWindowText(_presentBar, BuildBar(presentPercent));
            Win32.SetWindowText(_webViewState, webViewText);
            Win32.SetWindowText(_webViewBar, BuildBar(webViewPercent));
            Win32.SetWindowText(_rtssState, rtssText);
            Win32.SetWindowText(_rtssBar, BuildBar(rtssPercent));
            Win32.SetWindowText(_detail, detail);
        }

        private static string BuildBar(int percent)
        {
            const int cells = 42;
            int filled = Math.Clamp(percent * cells / 100, 0, cells);
            return "[" + new string('■', filled) + new string('·', cells - filled) + $"] {percent,3}%";
        }

        private void Finish(bool success)
        {
            _success = success;
            _finished = true;
            if (_window != 0)
            {
                if (success)
                {
                    Win32.PostMessage(_window, WmFinished, 0, 0);
                }
                else
                {
                    SetDetail("Не удалось подготовить компоненты. Закройте окно после проверки LOG.log.");
                }
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam)
        {
            DependencyStatusWindow? app = s_current;
            if (app is null)
            {
                return Win32.DefWindowProc(window, message, wParam, lParam);
            }

            try
            {
                switch (message)
                {
                    case Win32.WM_CREATE:
                        app.CreateControls(window);
                        return 0;

                    case WmProgress:
                        app.RenderProgress();
                        return 0;

                    case WmFinished:
                        Win32.DestroyWindow(window);
                        return 0;

                    case Win32.WM_CTLCOLORSTATIC:
                        uint previousTextColor = Win32.SetTextColor((nint)wParam, UiTheme.TextColor);
                        uint previousBackgroundColor = Win32.SetBkColor((nint)wParam, UiTheme.BackgroundColor);
                        if (previousTextColor == uint.MaxValue || previousBackgroundColor == uint.MaxValue)
                        {
                            return 0;
                        }
                        return app._theme?.BackgroundBrush ?? 0;

                    case Win32.WM_CLOSE:
                        if (app._finished)
                        {
                            Win32.DestroyWindow(window);
                        }
                        return 0;

                    case Win32.WM_DESTROY:
                        app._window = 0;
                        Win32.PostQuitMessage(0);
                        return 0;
                }
            }
            catch (Exception exception)
            {
                Logger.Error("Dependency status window procedure failed", exception);
            }
            return Win32.DefWindowProc(window, message, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_window != 0)
            {
                Win32.DestroyWindow(_window);
                _window = 0;
            }
            if (_instance != 0)
            {
                Win32.UnregisterClass(WindowClassName, _instance);
            }
            if (_className != 0)
            {
                Marshal.FreeHGlobal(_className);
                _className = 0;
            }
            _theme?.Dispose();
            _theme = null;
            s_current = null;
        }
    }
}

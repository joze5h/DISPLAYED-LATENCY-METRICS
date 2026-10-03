using DisplayedLatencyMetrics.App;
using DisplayedLatencyMetrics.Infrastructure;
using DisplayedLatencyMetrics.Interop;
using DisplayedLatencyMetrics.Metrics;
using DisplayedLatencyMetrics.Overlay;
using System.Diagnostics;

namespace DisplayedLatencyMetrics.Capture;

internal sealed unsafe class CaptureCoordinator : IDisposable
{

    private const int ReacquireAfterNoSamplesMilliseconds = 3_000;
    private const int InitialReacquireAfterNoSamplesMilliseconds = 8_000;

    private const int ForegroundTransitionGraceMilliseconds = 4_000;

    private const int LastSnapshotHoldMilliseconds = 4_000;
    private readonly AppSettings _settings;
    private readonly NativeOverlayWindow _overlay;
    private readonly Action<string> _statusChanged;
    private readonly Action<string, bool> _notification;
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _thread;
    private int _refreshRequested;
    private bool _rtssFailureReported;
    private MetricSnapshot? _lastSnapshot;
    private long _lastSnapshotAt;

    internal CaptureCoordinator(
        AppSettings settings,
        NativeOverlayWindow overlay,
        Action<string> statusChanged,
        Action<string, bool> notification)
    {
        _settings = settings;
        _overlay = overlay;
        _statusChanged = statusChanged;
        _notification = notification;
        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "PresentMon Capture",
            Priority = ThreadPriority.BelowNormal,
        };
    }

    internal void Start() => _thread.Start();

    internal void RequestRefresh() => Interlocked.Exchange(ref _refreshRequested, 1);

    private void ThreadMain()
    {
        try
        {
            string? apiPath = PresentMonRuntime.FindApiDll();
            if (apiPath is null)
            {
                _statusChanged("PresentMon API is not installed");
                _notification("PresentMon Service/API 2.5.1 is required.", true);
                return;
            }

            while (!_stop.IsCancellationRequested)
            {
                Process? process = FindNewestCs2();
                if (process is null)
                {
                    _statusChanged("Waiting for CS2");
                    ClearOutput();
                    SleepCancelable(1500);
                    continue;
                }

                ClearSnapshotCache();
                try
                {
                    while (!_stop.IsCancellationRequested && !HasExited(process))
                    {
                        CaptureRunOutcome outcome = RunCapture(apiPath, process);
                        if (outcome == CaptureRunOutcome.ProcessExited)
                        {
                            break;
                        }

                        _statusChanged("Display mode changed; reacquiring frame stream");
                        SleepCancelable(100);
                    }
                }
                catch (Exception exception)
                {
                    Logger.Error("Capture session failed", exception);
                    _statusChanged("Capture error");
                    _notification(exception.Message, true);
                    ClearOutput();
                    SleepCancelable(1500);
                }
                finally
                {
                    ClearSnapshotCache();
                    process.Dispose();
                }
            }
        }
        catch (Exception exception)
        {
            Logger.Error("Capture worker terminated", exception);
            _notification("Capture worker terminated. See LOG.log.", true);
        }
        finally
        {
            ClearOutput();
        }
    }

    private CaptureRunOutcome RunCapture(string apiPath, Process process)
    {
        uint pid = checked((uint)process.Id);
        _statusChanged($"CS2 detected · PID {pid}");
        using PresentMonApi api = PresentMonApi.Load(apiPath);

        PmStatus status = api.OpenSession(out nint session);
        if (status != PmStatus.Success || session == 0)
        {
            throw new InvalidOperationException($"pmOpenSession failed: {PresentMonStatus.Describe(status)}");
        }

        nint query = 0;
        bool tracking = false;
        try
        {
            PmStatus flushStatus = api.SetEtwFlushPeriod(session, 100);
            if (flushStatus != PmStatus.Success)
            {
                Logger.Warning($"pmSetEtwFlushPeriod(100) failed: {PresentMonStatus.Describe(flushStatus)}");
            }

            status = api.StartTrackingProcess(session, pid);
            if (status != PmStatus.Success && status != PmStatus.AlreadyTrackingProcess)
            {
                throw new InvalidOperationException($"pmStartTrackingProcess failed: {PresentMonStatus.Describe(status)}");
            }
            tracking = true;

            Dictionary<PmMetric, PmMetricInfo> metricInfo =
                api.ResolveMetricInfo(session, PresentMonFrameDecoder.RequiredMetrics);
            var decoder = new PresentMonFrameDecoder(metricInfo);

            status = api.RegisterFrameQuery(session, out query, decoder.Elements, out uint blobSize);
            if (status != PmStatus.Success || query == 0 || blobSize == 0)
            {
                throw new InvalidOperationException($"pmRegisterFrameQuery failed: {PresentMonStatus.Describe(status)}");
            }
            decoder.InitializeLayout(blobSize);

            return RunFrameLoop(api, query, blobSize, pid, process, decoder);
        }
        finally
        {
            if (query != 0)
            {
                api.FreeFrameQuery(query);
            }
            if (tracking)
            {
                api.StopTrackingProcess(session, pid);
            }
            api.CloseSession(session);
        }
    }

    private CaptureRunOutcome RunFrameLoop(
        PresentMonApi api,
        nint query,
        uint blobSize,
        uint pid,
        Process process,
        PresentMonFrameDecoder decoder)
    {
        uint capacity = 4096;
        byte[] buffer = AllocateBuffer(blobSize, capacity);
        var statistics = new FrameMetricsPipeline(1000.0);
        SettingsSnapshot settings = _settings.Snapshot();
        long now = Stopwatch.GetTimestamp();

        long nextPublish = now;
        long nextForegroundCheck = now;
        long nextProcessCheck = now + Stopwatch.Frequency / 2;
        long nextSettingsCheck = now;
        nint foregroundWindow = 0;
        nint lastKnownGameWindow = 0;
        bool processExited = false;
        bool wasCollecting = false;
        bool wasOutputEligible = false;
        bool hasSeenForeground = false;
        long lastForegroundSeenAt = 0;
        bool receivedUsableSamples = false;
        long collectionStartedAt = now;
        long lastUsableSampleAt = now;

        _statusChanged("Collecting CS2 frame metrics");

        while (!_stop.IsCancellationRequested && !processExited)
        {
            now = Stopwatch.GetTimestamp();
            bool refreshRequested = Interlocked.Exchange(ref _refreshRequested, 0) != 0;
            if (refreshRequested || now >= nextSettingsCheck)
            {
                settings = _settings.Snapshot();
                nextSettingsCheck = now + Stopwatch.Frequency / 4;
                if (refreshRequested)
                {
                    nextPublish = now;
                }
            }

            if (now >= nextForegroundCheck)
            {
                nint currentForegroundWindow = GetForegroundWindowForProcess(pid);
                if (currentForegroundWindow != 0)
                {
                    hasSeenForeground = true;
                    lastForegroundSeenAt = now;
                    lastKnownGameWindow = currentForegroundWindow;

                    if (foregroundWindow != currentForegroundWindow)
                    {
                        nextPublish = now;
                    }
                }
                else if (foregroundWindow != 0)
                {

                    _overlay.Clear();
                }

                foregroundWindow = currentForegroundWindow;
                nextForegroundCheck = now + Stopwatch.Frequency / 10;
            }
            if (now >= nextProcessCheck)
            {
                processExited = HasExited(process);
                nextProcessCheck = now + Stopwatch.Frequency / 2;
            }

            bool inForegroundTransition = hasSeenForeground &&
                now - lastForegroundSeenAt <= ToStopwatchTicks(ForegroundTransitionGraceMilliseconds);
            bool lastWindowUsable = lastKnownGameWindow != 0 &&
                Win32.IsWindow(lastKnownGameWindow) &&
                Win32.IsWindowVisible(lastKnownGameWindow) &&
                !Win32.IsIconic(lastKnownGameWindow);
            nint outputWindow = foregroundWindow != 0
                ? foregroundWindow
                : inForegroundTransition && lastWindowUsable
                    ? lastKnownGameWindow
                    : 0;

            bool outputEligible = outputWindow != 0;
            if (wasOutputEligible && !outputEligible && settings.OutputMode == OverlayOutputMode.NativeWindow)
            {
                _overlay.Clear();
            }
            wasOutputEligible = outputEligible;

            bool collecting = settings.OutputMode == OverlayOutputMode.Rtss || outputEligible;
            statistics.SetCaptureActive(collecting);
            if (collecting && !wasCollecting)
            {
                collectionStartedAt = now;
                lastUsableSampleAt = now;
            }
            wasCollecting = collecting;

            uint frames = capacity;
            PmStatus consumeStatus;
            bool receivedUsableSampleThisPass = false;
            fixed (byte* bufferPointer = buffer)
            {
                consumeStatus = api.ConsumeFrames(query, pid, bufferPointer, ref frames);
                if (consumeStatus == PmStatus.Success && collecting)
                {
                    for (uint index = 0; index < frames; index++)
                    {
                        byte* blob = bufferPointer + checked((int)(index * blobSize));
                        if (!decoder.TryDecode(blob, blobSize, out ulong swapChain, out FrameSample sample))
                        {
                            continue;
                        }

                        statistics.Add(swapChain, sample);
                        receivedUsableSampleThisPass = true;
                    }
                }
            }

            if (consumeStatus == PmStatus.InsufficientBuffer)
            {
                if (frames > 65_536U)
                {
                    throw new InvalidOperationException("PresentMon frame backlog exceeded 65536 records.");
                }
                uint requested = Math.Max(frames, capacity > 32_768U ? 65_536U : capacity * 2U);
                capacity = Math.Min(requested, 65_536U);
                buffer = AllocateBuffer(blobSize, capacity);
                continue;
            }
            if (consumeStatus != PmStatus.Success)
            {
                if (consumeStatus == PmStatus.InvalidPid && (processExited || HasExited(process)))
                {
                    return CaptureRunOutcome.ProcessExited;
                }
                throw new InvalidOperationException($"pmConsumeFrames failed: {PresentMonStatus.Describe(consumeStatus)}");
            }

            now = Stopwatch.GetTimestamp();
            if (receivedUsableSampleThisPass)
            {
                receivedUsableSamples = true;
                lastUsableSampleAt = now;
            }
            else if (collecting)
            {
                long limit = Stopwatch.Frequency *
                    (receivedUsableSamples
                        ? ReacquireAfterNoSamplesMilliseconds
                        : InitialReacquireAfterNoSamplesMilliseconds) / 1000;
                long reference = receivedUsableSamples ? lastUsableSampleAt : collectionStartedAt;
                if (now - reference >= limit)
                {
                    Logger.Warning("PresentMon frame query stopped producing usable samples; recreating it.");
                    return CaptureRunOutcome.Reacquire;
                }
            }
            if (now >= nextPublish)
            {
                Publish(
                    statistics,
                    outputWindow,
                    foregroundWindow != 0,
                    settings,
                    now);
                nextPublish = now + ToStopwatchTicks(settings.Presentation.RefreshIntervalMilliseconds);
            }

            SleepCancelable(16);
        }

        return CaptureRunOutcome.ProcessExited;
    }

    private enum CaptureRunOutcome
    {
        ProcessExited,
        Reacquire,
    }

    private void Publish(
        FrameMetricsPipeline statistics,
        nint targetWindow,
        bool gameIsForeground,
        SettingsSnapshot settings,
        long now)
    {
        if (!settings.OverlayEnabled)
        {
            ClearOutput();
            _statusChanged("Metrics collecting; output disabled");
            return;
        }

        if (settings.OutputMode == OverlayOutputMode.NativeWindow)
        {
            RtssOverlayPublisher.Clear();
            if (targetWindow == 0)
            {
                _overlay.Clear();
                _statusChanged("CS2 is not foreground");
                return;
            }

            if (!gameIsForeground)
            {
                _overlay.Clear();
                _statusChanged("CS2 display mode is changing");
                return;
            }
        }

        MetricSnapshot? snapshot = statistics.Calculate();
        if (snapshot is not null)
        {
            _lastSnapshot = snapshot;
            _lastSnapshotAt = now;
        }
        else if (_lastSnapshot is not null &&
                 now - _lastSnapshotAt <= ToStopwatchTicks(LastSnapshotHoldMilliseconds))
        {
            snapshot = _lastSnapshot;
        }

        OverlayDocument document = snapshot is null
            ? OverlayFormatter.BuildStatus("Reacquiring frame stream…", settings)
            : OverlayFormatter.Build(snapshot, settings);
        PublishDocument(
            document,
            targetWindow,
            settings,
            snapshot is null ? "Reacquiring frame stream" : "Metrics output active");
    }

    private void PublishDocument(
        OverlayDocument document,
        nint foregroundWindow,
        SettingsSnapshot settings,
        string status)
    {
        if (settings.OutputMode == OverlayOutputMode.Rtss)
        {
            _overlay.Clear();
            if (RtssOverlayPublisher.TryPublish(document, out string problem))
            {
                _rtssFailureReported = false;
                _statusChanged($"RTSS OSD active in CS2 · {status}");
                return;
            }

            _statusChanged($"RTSS unavailable: {problem}");
            if (!_rtssFailureReported)
            {
                _rtssFailureReported = true;
                Logger.Warning($"RTSS OSD output failed: {problem}");
                _notification($"RTSS OSD: {problem}", true);
            }
            return;
        }

        RtssOverlayPublisher.Clear();
        _rtssFailureReported = false;
        _overlay.Publish(foregroundWindow, document, settings.Presentation);
        _statusChanged($"Native overlay active in CS2 · {status}");
    }

    private void ClearOutput()
    {
        _overlay.Clear();
        RtssOverlayPublisher.Clear();
    }

    private void ClearSnapshotCache()
    {
        _lastSnapshot = null;
        _lastSnapshotAt = 0;
    }

    private static long ToStopwatchTicks(int milliseconds) =>
        Math.Max(1L, Stopwatch.Frequency * (long)Math.Max(1, milliseconds) / 1000L);

    private static byte[] AllocateBuffer(uint blobSize, uint capacity)
    {
        ulong length = (ulong)blobSize * capacity;
        if (length > int.MaxValue)
        {
            throw new InvalidOperationException("PresentMon frame buffer is too large.");
        }
        return GC.AllocateUninitializedArray<byte>((int)length, pinned: true);
    }

    private static Process? FindNewestCs2()
    {
        Process? selected = null;
        DateTime selectedStart = DateTime.MinValue;
        foreach (Process process in Process.GetProcessesByName("cs2"))
        {
            try
            {
                DateTime start = process.StartTime;
                if (selected is null || start > selectedStart)
                {
                    selected?.Dispose();
                    selected = process;
                    selectedStart = start;
                }
                else
                {
                    process.Dispose();
                }
            }
            catch
            {
                process.Dispose();
            }
        }
        return selected;
    }

    private static nint GetForegroundWindowForProcess(uint pid)
    {
        nint foreground = Win32.GetForegroundWindow();
        return foreground != 0 &&
               Win32.GetWindowThreadProcessId(foreground, out uint foregroundPid) != 0 &&
               foregroundPid == pid
            ? foreground
            : 0;
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception exception)
        {
            Logger.Warning("Unable to query whether the target process has exited; treating it as exited.", exception);
            return true;
        }
    }

    private void SleepCancelable(int milliseconds) => _stop.Token.WaitHandle.WaitOne(milliseconds);

    public void Dispose()
    {
        _stop.Cancel();
        bool stopped = !_thread.IsAlive || _thread.Join(TimeSpan.FromSeconds(4));
        if (stopped)
        {
            _stop.Dispose();
        }
        else
        {
            Logger.Warning("Capture worker did not stop within four seconds; cancellation state is retained until process exit.");
        }
        GC.SuppressFinalize(this);
    }
}

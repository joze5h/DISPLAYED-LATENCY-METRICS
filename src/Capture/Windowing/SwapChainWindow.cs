using DisplayedLatencyMetrics.Interop;
using DisplayedLatencyMetrics.Metrics;
using System.Diagnostics;

namespace DisplayedLatencyMetrics.Capture.Windowing;

internal sealed class SwapChainWindow
{
    private readonly Queue<FrameSample> _samples = new();
    private bool _hasPresentationState;

    internal SwapChainWindow(ulong id)
    {
        Id = id;
    }

    internal ulong Id { get; }

    internal IReadOnlyCollection<FrameSample> Samples => _samples;

    internal PmPresentMode PresentMode { get; private set; }

    internal PmGraphicsRuntime PresentRuntime { get; private set; }

    internal int SyncInterval { get; private set; }

    internal bool AllowsTearing { get; private set; }

    internal void Add(FrameSample source)
    {
        FrameSample sample = ApplyPresentationState(source, out bool presentationStateChanged);
        bool applicationFrame = IsApplicationFrame(sample.FrameType);

        double presentedInterval =
            applicationFrame &&
            !presentationStateChanged &&
            StatisticsMath.IsPositiveFinite(sample.BetweenPresentsMs)
                ? sample.BetweenPresentsMs
                : double.NaN;

        double displayedInterval =
            !presentationStateChanged &&
            StatisticsMath.IsPositiveFinite(sample.BetweenDisplayChangeMs)
                ? sample.BetweenDisplayChangeMs
                : double.NaN;

        ulong timestamp = sample.TimestampQpc != 0
            ? sample.TimestampQpc
            : unchecked((ulong)Stopwatch.GetTimestamp());
        _samples.Enqueue(sample with
        {
            TimestampQpc = timestamp,
            BetweenAppStartMs = applicationFrame ? sample.BetweenAppStartMs : double.NaN,
            CpuBusyMs = applicationFrame ? sample.CpuBusyMs : double.NaN,
            CpuWaitMs = applicationFrame ? sample.CpuWaitMs : double.NaN,
            GpuBusyMs = applicationFrame ? sample.GpuBusyMs : double.NaN,
            GpuTimeMs = applicationFrame ? sample.GpuTimeMs : double.NaN,
            GpuWaitMs = applicationFrame ? sample.GpuWaitMs : double.NaN,
            GpuLatencyMs = applicationFrame ? sample.GpuLatencyMs : double.NaN,
            DisplayLatencyMs = applicationFrame && StatisticsMath.IsNonNegativeFinite(sample.DisplayLatencyMs)
                ? sample.DisplayLatencyMs
                : double.NaN,
            UntilDisplayedMs = StatisticsMath.IsNonNegativeFinite(sample.UntilDisplayedMs)
                ? sample.UntilDisplayedMs
                : double.NaN,
            BetweenDisplayChangeMs = displayedInterval,
            BetweenPresentsMs = presentedInterval,
        });
    }

    internal void Prune(ulong oldestAllowedTimestamp)
    {
        while (_samples.Count > 0 && _samples.Peek().TimestampQpc < oldestAllowedTimestamp)
        {
            _samples.Dequeue();
        }
    }

    internal void ClearSamples() => _samples.Clear();

    private FrameSample ApplyPresentationState(
        FrameSample sample,
        out bool presentationStateChanged)
    {
        PmPresentMode effectiveMode = sample.PresentMode == PmPresentMode.Unknown && _hasPresentationState
            ? PresentMode
            : sample.PresentMode;
        PmGraphicsRuntime effectiveRuntime = sample.PresentRuntime == PmGraphicsRuntime.Unknown && _hasPresentationState
            ? PresentRuntime
            : sample.PresentRuntime;

        presentationStateChanged = _hasPresentationState &&
            (effectiveMode != PresentMode || effectiveRuntime != PresentRuntime);
        if (presentationStateChanged)
        {

            ClearSamples();
        }

        _hasPresentationState = true;
        PresentMode = effectiveMode;
        PresentRuntime = effectiveRuntime;
        SyncInterval = sample.SyncInterval;
        AllowsTearing = sample.AllowsTearing;
        return sample with
        {
            PresentMode = effectiveMode,
            PresentRuntime = effectiveRuntime,
        };
    }

    private static bool IsApplicationFrame(PmFrameType type) => type is
        PmFrameType.NotSet or PmFrameType.Unspecified or PmFrameType.Application;
}

using DisplayedLatencyMetrics.Capture.Windowing;
using DisplayedLatencyMetrics.Metrics;

namespace DisplayedLatencyMetrics.Capture;

internal sealed class MetricContextBuilder
{
    private readonly List<double> _msBetweenPresentsValues = new(1024);
    private readonly List<double> _displayLatencyValues = new(1024);
    private readonly List<double> _msUntilDisplayedValues = new(1024);
    private readonly List<double> _msBetweenDisplayChangeValues = new(1024);

    internal MetricContext Build(SwapChainWindow window)
    {
        _msBetweenPresentsValues.Clear();
        _displayLatencyValues.Clear();
        _msUntilDisplayedValues.Clear();
        _msBetweenDisplayChangeValues.Clear();

        var msBetweenAppStart = new RunningStatistics();
        var msCpuBusy = new RunningStatistics();
        var msCpuWait = new RunningStatistics();
        var msGpuBusy = new RunningStatistics();
        var msGpuTime = new RunningStatistics();
        var msGpuWait = new RunningStatistics();
        var msGpuLatency = new RunningStatistics();
        var displayLatency = new RunningStatistics();
        var msBetweenPresents = new RunningStatistics();
        var msUntilDisplayed = new RunningStatistics();
        var msBetweenDisplayChange = new RunningStatistics();

        foreach (FrameSample sample in window.Samples)
        {
            AddNonNegative(ref msBetweenAppStart, sample.BetweenAppStartMs);
            AddNonNegative(ref msCpuBusy, sample.CpuBusyMs);
            AddNonNegative(ref msCpuWait, sample.CpuWaitMs);
            AddNonNegative(ref msGpuBusy, sample.GpuBusyMs);
            AddNonNegative(ref msGpuTime, sample.GpuTimeMs);
            AddNonNegative(ref msGpuWait, sample.GpuWaitMs);
            AddNonNegative(ref msGpuLatency, sample.GpuLatencyMs);

            if (StatisticsMath.IsNonNegativeFinite(sample.DisplayLatencyMs))
            {
                _displayLatencyValues.Add(sample.DisplayLatencyMs);
                displayLatency.Add(sample.DisplayLatencyMs);
            }

            if (StatisticsMath.IsPositiveFinite(sample.BetweenPresentsMs))
            {
                _msBetweenPresentsValues.Add(sample.BetweenPresentsMs);
                msBetweenPresents.Add(sample.BetweenPresentsMs);
            }

            if (StatisticsMath.IsNonNegativeFinite(sample.UntilDisplayedMs))
            {
                _msUntilDisplayedValues.Add(sample.UntilDisplayedMs);
                msUntilDisplayed.Add(sample.UntilDisplayedMs);
            }

            if (StatisticsMath.IsPositiveFinite(sample.BetweenDisplayChangeMs))
            {
                _msBetweenDisplayChangeValues.Add(sample.BetweenDisplayChangeMs);
                msBetweenDisplayChange.Add(sample.BetweenDisplayChangeMs);
            }
        }

        return new MetricContext
        {
            SwapChain = window.Id,
            PresentMode = window.PresentMode,
            PresentRuntime = window.PresentRuntime,
            SyncInterval = window.SyncInterval,
            AllowsTearing = window.AllowsTearing,
            MsBetweenAppStart = msBetweenAppStart.Snapshot(),
            MsCPUBusy = msCpuBusy.Snapshot(),
            MsCPUWait = msCpuWait.Snapshot(),
            MsGPUBusy = msGpuBusy.Snapshot(),
            MsGPUTime = msGpuTime.Snapshot(),
            MsGPUWait = msGpuWait.Snapshot(),
            MsGPULatency = msGpuLatency.Snapshot(),
            DisplayLatency = displayLatency.Snapshot(),
            MsBetweenPresents = msBetweenPresents.Snapshot(),
            MsUntilDisplayed = msUntilDisplayed.Snapshot(),
            MsBetweenDisplayChange = msBetweenDisplayChange.Snapshot(),
            MsBetweenPresentsValues = _msBetweenPresentsValues,
            DisplayLatencyValues = _displayLatencyValues,
            MsUntilDisplayedValues = _msUntilDisplayedValues,
            MsBetweenDisplayChangeValues = _msBetweenDisplayChangeValues,
            CapFrameXStutter = CapFrameXStutterAnalysis.Analyze(_msBetweenPresentsValues),
        };
    }

    private static void AddNonNegative(ref RunningStatistics statistics, double value)
    {
        if (StatisticsMath.IsNonNegativeFinite(value))
        {
            statistics.Add(value);
        }
    }
}

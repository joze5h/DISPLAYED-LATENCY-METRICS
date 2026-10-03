using DisplayedLatencyMetrics.Capture.Windowing;
using DisplayedLatencyMetrics.Metrics;

namespace DisplayedLatencyMetrics.Capture;

internal sealed class FrameMetricsPipeline
{
    private readonly FrameWindowStore _windows;
    private readonly MetricContextBuilder _contextBuilder = new();

    internal FrameMetricsPipeline(double windowMilliseconds = 1000.0)
    {
        _windows = new FrameWindowStore(windowMilliseconds);
    }

    internal void Reset() => _windows.Reset();

    internal void SetCaptureActive(bool active) => _windows.SetCaptureActive(active);

    internal void Add(ulong swapChain, FrameSample sample) => _windows.Add(swapChain, sample);

    internal MetricSnapshot? Calculate()
    {
        SwapChainWindow? selected = SwapChainSelector.Select(_windows.GetActiveWindows());
        if (selected is null)
        {
            return null;
        }

        MetricContext context = _contextBuilder.Build(selected);
        return MetricEngine.Calculate(context);
    }
}

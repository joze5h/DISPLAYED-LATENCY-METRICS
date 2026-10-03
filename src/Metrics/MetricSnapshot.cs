using DisplayedLatencyMetrics.Interop;

namespace DisplayedLatencyMetrics.Metrics;

internal sealed class MetricSnapshot
{
    private readonly MetricValue[] _values;

    internal MetricSnapshot(
        ulong swapChain,
        PmPresentMode presentMode,
        PmGraphicsRuntime presentRuntime,
        int syncInterval,
        bool allowsTearing,
        MetricValue[] values)
    {
        if (values.Length != MetricRegistry.Count)
        {
            throw new ArgumentException("Metric result count does not match the registry.", nameof(values));
        }

        SwapChain = swapChain;
        PresentMode = presentMode;
        PresentRuntime = presentRuntime;
        SyncInterval = syncInterval;
        AllowsTearing = allowsTearing;
        _values = values;
    }

    internal ulong SwapChain { get; }

    internal PmPresentMode PresentMode { get; }

    internal PmGraphicsRuntime PresentRuntime { get; }

    internal int SyncInterval { get; }

    internal bool AllowsTearing { get; }

    internal MetricValue Get(MetricFlags flag)
    {
        int index = MetricRegistry.BitIndex(flag);
        return index >= 0 ? _values[index] : MetricValue.Unavailable;
    }
}

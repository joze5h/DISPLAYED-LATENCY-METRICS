namespace DisplayedLatencyMetrics.Metrics;

internal static class MetricEngine
{
    internal static MetricSnapshot Calculate(MetricContext context)
    {
        var values = new MetricValue[MetricRegistry.Count];
        foreach (IMetric metric in MetricRegistry.All)
        {
            int index = MetricRegistry.BitIndex(metric.Flag);
            values[index] = metric.Calculate(context);
        }

        return new MetricSnapshot(
            context.SwapChain,
            context.PresentMode,
            context.PresentRuntime,
            context.SyncInterval,
            context.AllowsTearing,
            values);
    }
}

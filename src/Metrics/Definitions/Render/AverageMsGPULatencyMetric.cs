namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class AverageMsGPULatencyMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsGPULatency;

    public override string Name => "Average MsGPULatency";

    public override MetricGroup Group => MetricGroup.Render;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(context.MsGPULatency.Average, context.MsGPULatency.Count);
}

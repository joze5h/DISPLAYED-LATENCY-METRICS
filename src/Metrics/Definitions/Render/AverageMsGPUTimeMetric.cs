namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class AverageMsGPUTimeMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsGPUTime;

    public override string Name => "Average MsGPUTime";

    public override MetricGroup Group => MetricGroup.Render;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(context.MsGPUTime.Average, context.MsGPUTime.Count);
}

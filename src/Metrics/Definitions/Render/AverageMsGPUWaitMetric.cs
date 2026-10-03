namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class AverageMsGPUWaitMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsGPUWait;

    public override string Name => "Average MsGPUWait";

    public override MetricGroup Group => MetricGroup.Render;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(context.MsGPUWait.Average, context.MsGPUWait.Count);
}

namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class AverageMsGPUBusyMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsGPUBusy;

    public override string Name => "Average MsGPUBusy";

    public override MetricGroup Group => MetricGroup.Render;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(context.MsGPUBusy.Average, context.MsGPUBusy.Count);
}

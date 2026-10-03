namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class CpuBusyMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsCPUBusy;

    public override string Name => "Average MsCPUBusy";

    public override MetricGroup Group => MetricGroup.Cpu;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(context.MsCPUBusy.Average, context.MsCPUBusy.Count);
}

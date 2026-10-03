namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class CpuWaitMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsCPUWait;

    public override string Name => "Average MsCPUWait";

    public override MetricGroup Group => MetricGroup.Cpu;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(context.MsCPUWait.Average, context.MsCPUWait.Count);
}

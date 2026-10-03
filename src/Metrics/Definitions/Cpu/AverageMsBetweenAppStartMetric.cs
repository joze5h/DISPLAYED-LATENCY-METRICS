namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class AverageMsBetweenAppStartMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsBetweenAppStart;

    public override string Name => "Average MsBetweenAppStart";

    public override MetricGroup Group => MetricGroup.Cpu;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(context.MsBetweenAppStart.Average, context.MsBetweenAppStart.Count);
}

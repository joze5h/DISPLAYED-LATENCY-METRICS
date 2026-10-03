namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class AverageMsBetweenDisplayChangeMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsBetweenDisplayChange;

    public override string Name => "Average MsBetweenDisplayChange";

    public override MetricGroup Group => MetricGroup.Display;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(
            context.MsBetweenDisplayChange.Average,
            context.MsBetweenDisplayChange.Count);
}

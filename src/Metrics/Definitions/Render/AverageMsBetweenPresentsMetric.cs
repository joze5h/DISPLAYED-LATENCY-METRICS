namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class AverageMsBetweenPresentsMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsBetweenPresents;

    public override string Name => "Average MsBetweenPresents";

    public override MetricGroup Group => MetricGroup.Render;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(context.MsBetweenPresents.Average, context.MsBetweenPresents.Count);
}

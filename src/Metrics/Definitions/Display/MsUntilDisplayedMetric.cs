namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class MsUntilDisplayedMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsUntilDisplayed;

    public override string Name => "Average MsUntilDisplayed";

    public override MetricGroup Group => MetricGroup.Display;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(context.MsUntilDisplayed.Average, context.MsUntilDisplayed.Count);
}

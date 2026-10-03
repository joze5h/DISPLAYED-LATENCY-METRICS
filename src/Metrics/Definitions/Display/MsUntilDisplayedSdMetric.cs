namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class MsUntilDisplayedSdMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsUntilDisplayedSd;

    public override string Name => "SD MsUntilDisplayed";

    public override MetricGroup Group => MetricGroup.Display;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(
            context.MsUntilDisplayed.PopulationSd,
            context.MsUntilDisplayed.Count);
}

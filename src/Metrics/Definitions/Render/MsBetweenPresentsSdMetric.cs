namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class MsBetweenPresentsSdMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsBetweenPresentsSd;

    public override string Name => "SD MsBetweenPresents";

    public override MetricGroup Group => MetricGroup.Render;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(
            context.MsBetweenPresents.PopulationSd,
            context.MsBetweenPresents.Count);
}

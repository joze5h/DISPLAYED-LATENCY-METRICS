namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class MsBetweenDisplayChangeSdMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsBetweenDisplayChangeSd;

    public override string Name => "SD MsBetweenDisplayChange";

    public override MetricGroup Group => MetricGroup.Display;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(
            context.MsBetweenDisplayChange.PopulationSd,
            context.MsBetweenDisplayChange.Count);
}

namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class DisplayedStepwiseRelativeMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsBetweenDisplayChangeStepwiseRelative;

    public override string Name => "Displayed Stepwise-Relative";

    public override MetricGroup Group => MetricGroup.Display;

    protected override string Unit => "%";

    protected override int MaximumDecimalPlaces => 2;

    public override MetricValue Calculate(MetricContext context)
    {
        double value = StatisticsMath.StepwiseRelative(
            context.MsBetweenDisplayChangeValues,
            allowZero: false,
            out int pairs);
        return MetricValue.Numeric(value, pairs);
    }
}

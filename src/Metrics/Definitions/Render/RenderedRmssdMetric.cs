namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class RenderedRmssdMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsBetweenPresentsRmssd;

    public override string Name => "RMSSD MsBetweenPresents";

    public override MetricGroup Group => MetricGroup.Render;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context)
    {
        double value = StatisticsMath.Rmssd(
            context.MsBetweenPresentsValues,
            allowZero: false,
            out int pairs);
        return MetricValue.Numeric(value, pairs);
    }
}

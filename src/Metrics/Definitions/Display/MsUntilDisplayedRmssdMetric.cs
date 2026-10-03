namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class MsUntilDisplayedRmssdMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsUntilDisplayedRmssd;

    public override string Name => "RMSSD MsUntilDisplayed";

    public override MetricGroup Group => MetricGroup.Display;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context)
    {
        double value = StatisticsMath.Rmssd(
            context.MsUntilDisplayedValues,
            allowZero: true,
            out int pairs);
        return MetricValue.Numeric(value, pairs);
    }
}

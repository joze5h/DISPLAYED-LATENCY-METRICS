namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class DisplayedRmssdMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.MsBetweenDisplayChangeRmssd;

    public override string Name => "RMSSD MsBetweenDisplayChange";

    public override MetricGroup Group => MetricGroup.Display;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context)
    {
        double value = StatisticsMath.Rmssd(
            context.MsBetweenDisplayChangeValues,
            allowZero: false,
            out int pairs);
        return MetricValue.Numeric(value, pairs);
    }
}

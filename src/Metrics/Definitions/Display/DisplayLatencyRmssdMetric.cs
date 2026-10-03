namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class DisplayLatencyRmssdMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.DisplayLatencyRmssd;

    public override string Name => "RMSSD DisplayLatency";

    public override MetricGroup Group => MetricGroup.Display;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context)
    {
        double value = StatisticsMath.Rmssd(
            context.DisplayLatencyValues,
            allowZero: true,
            out int pairs);
        return MetricValue.Numeric(value, pairs);
    }
}

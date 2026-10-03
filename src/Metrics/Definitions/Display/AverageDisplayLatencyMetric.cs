namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class AverageDisplayLatencyMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.DisplayLatency;

    public override string Name => "Average DisplayLatency";

    public override MetricGroup Group => MetricGroup.Display;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(context.DisplayLatency.Average, context.DisplayLatency.Count);
}

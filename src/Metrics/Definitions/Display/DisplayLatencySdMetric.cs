namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class DisplayLatencySdMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.DisplayLatencySd;

    public override string Name => "SD DisplayLatency";

    public override MetricGroup Group => MetricGroup.Display;

    protected override string Unit => " ms";

    public override MetricValue Calculate(MetricContext context) =>
        MetricValue.Numeric(context.DisplayLatency.PopulationSd, context.DisplayLatency.Count);
}

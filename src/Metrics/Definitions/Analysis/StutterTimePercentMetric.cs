namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class StutterTimePercentMetric : NumericMetric
{
    public override MetricFlags Flag => MetricFlags.StutterTimePercentCapFrameX;

    public override string Name => "Stutter Time % [CapFrameX]";

    public override MetricGroup Group => MetricGroup.Analysis;

    protected override string Unit => "%";

    protected override int MaximumDecimalPlaces => 2;

    public override MetricValue Calculate(MetricContext context)
    {
        CapFrameXStutterResult result = context.CapFrameXStutter;
        return result.TotalFrames > 0
            ? MetricValue.Events(
                result.TimePercentage,
                result.StutterFrames,
                result.TotalFrames)
            : MetricValue.Unavailable;
    }
}

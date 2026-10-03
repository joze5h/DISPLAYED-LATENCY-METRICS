using System.Globalization;

namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class StutterFramesMetric : IMetric
{
    public MetricFlags Flag => MetricFlags.StutterFramesCapFrameX;

    public string Name => "Stutter Frames [CapFrameX]";

    public MetricGroup Group => MetricGroup.Analysis;

    public MetricValue Calculate(MetricContext context)
    {
        CapFrameXStutterResult result = context.CapFrameXStutter;
        return result.TotalFrames > 0
            ? MetricValue.Events(
                result.TimePercentage,
                result.StutterFrames,
                result.TotalFrames)
            : MetricValue.Unavailable;
    }

    public string Format(MetricValue value, MetricFormatOptions options)
    {
        if (value.SampleCount <= 0)
        {
            return "N/A";
        }

        return string.Concat(
            value.EventCount.ToString(CultureInfo.InvariantCulture),
            " / ",
            value.SampleCount.ToString(CultureInfo.InvariantCulture));
    }
}

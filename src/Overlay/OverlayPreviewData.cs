using DisplayedLatencyMetrics.Interop;
using DisplayedLatencyMetrics.Metrics;

namespace DisplayedLatencyMetrics.Overlay;

internal static class OverlayPreviewData
{
    internal static MetricSnapshot Create()
    {
        var values = new MetricValue[MetricRegistry.Count];
        Set(values, MetricFlags.PresentMode, MetricValue.Textual("Hardware: Independent Flip"));
        Set(values, MetricFlags.MsBetweenAppStart, MetricValue.Numeric(4.167, 240));
        Set(values, MetricFlags.MsCPUBusy, MetricValue.Numeric(2.842, 240));
        Set(values, MetricFlags.MsCPUWait, MetricValue.Numeric(1.325, 240));
        Set(values, MetricFlags.MsGPUBusy, MetricValue.Numeric(3.462, 240));
        Set(values, MetricFlags.MsGPUTime, MetricValue.Numeric(3.711, 240));
        Set(values, MetricFlags.MsGPUWait, MetricValue.Numeric(0.249, 240));
        Set(values, MetricFlags.MsGPULatency, MetricValue.Numeric(1.104, 240));
        Set(values, MetricFlags.MsBetweenPresents, MetricValue.Numeric(4.167, 239));
        Set(values, MetricFlags.MsBetweenPresentsSd, MetricValue.Numeric(0.286, 239));
        Set(values, MetricFlags.MsBetweenPresentsRmssd, MetricValue.Numeric(0.412, 238));
        Set(values, MetricFlags.StutterTimePercentCapFrameX, MetricValue.Events(1.49, 3, 239));
        Set(values, MetricFlags.StutterFramesCapFrameX, MetricValue.Events(1.49, 3, 239));
        Set(values, MetricFlags.DisplayLatency, MetricValue.Numeric(8.614, 238));
        Set(values, MetricFlags.DisplayLatencySd, MetricValue.Numeric(0.731, 238));
        Set(values, MetricFlags.DisplayLatencyRmssd, MetricValue.Numeric(0.946, 237));
        Set(values, MetricFlags.MsUntilDisplayed, MetricValue.Numeric(5.293, 238));
        Set(values, MetricFlags.MsUntilDisplayedSd, MetricValue.Numeric(0.295, 238));
        Set(values, MetricFlags.MsUntilDisplayedRmssd, MetricValue.Numeric(0.367, 237));
        Set(values, MetricFlags.MsBetweenDisplayChange, MetricValue.Numeric(4.167, 238));
        Set(values, MetricFlags.MsBetweenDisplayChangeSd, MetricValue.Numeric(0.621, 238));
        Set(values, MetricFlags.MsBetweenDisplayChangeRmssd, MetricValue.Numeric(0.835, 237));
        Set(values, MetricFlags.MsBetweenDisplayChangeStepwiseRelative, MetricValue.Numeric(32.42, 237));

        return new MetricSnapshot(
            0,
            PmPresentMode.HardwareIndependentFlip,
            PmGraphicsRuntime.Dxgi,
            0,
            true,
            values);
    }

    private static void Set(MetricValue[] values, MetricFlags flag, MetricValue value) =>
        values[MetricRegistry.BitIndex(flag)] = value;
}

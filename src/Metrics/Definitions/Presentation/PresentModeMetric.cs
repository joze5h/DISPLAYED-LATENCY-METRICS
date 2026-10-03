using DisplayedLatencyMetrics.Interop;

namespace DisplayedLatencyMetrics.Metrics.Definitions;

internal sealed class PresentModeMetric : IMetric
{
    public MetricFlags Flag => MetricFlags.PresentMode;

    public string Name => "PresentMode";

    public MetricGroup Group => MetricGroup.Presentation;

    public MetricValue Calculate(MetricContext context) =>
        MetricValue.Textual(DisplayName(context.PresentMode));

    public string Format(MetricValue value, MetricFormatOptions options) =>
        value.HasText ? value.Text! : "Unknown";

    private static string DisplayName(PmPresentMode mode) => mode switch
    {
        PmPresentMode.Unknown => "Unknown",
        PmPresentMode.HardwareLegacyFlip => "Hardware: Legacy Flip",
        PmPresentMode.HardwareLegacyCopyToFrontBuffer => "Hardware: Legacy Copy to front buffer",
        PmPresentMode.HardwareIndependentFlip => "Hardware: Independent Flip",
        PmPresentMode.ComposedFlip => "Composed: Flip",
        PmPresentMode.ComposedCopyWithGpuGdi => "Composed: Copy with GPU GDI",
        PmPresentMode.ComposedCopyWithCpuGdi => "Composed: Copy with CPU GDI",
        PmPresentMode.HardwareComposedIndependentFlip => "Hardware Composed: Independent Flip",
        _ => "Unknown",
    };
}

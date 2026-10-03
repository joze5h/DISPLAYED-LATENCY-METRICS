using DisplayedLatencyMetrics.Interop;

namespace DisplayedLatencyMetrics.Metrics;

internal sealed class MetricContext
{
    internal required ulong SwapChain { get; init; }

    internal required PmPresentMode PresentMode { get; init; }

    internal required PmGraphicsRuntime PresentRuntime { get; init; }

    internal required int SyncInterval { get; init; }

    internal required bool AllowsTearing { get; init; }

    internal required ScalarStatistics MsBetweenAppStart { get; init; }

    internal required ScalarStatistics MsCPUBusy { get; init; }

    internal required ScalarStatistics MsCPUWait { get; init; }

    internal required ScalarStatistics MsGPUBusy { get; init; }

    internal required ScalarStatistics MsGPUTime { get; init; }

    internal required ScalarStatistics MsGPUWait { get; init; }

    internal required ScalarStatistics MsGPULatency { get; init; }

    internal required ScalarStatistics DisplayLatency { get; init; }

    internal required ScalarStatistics MsBetweenPresents { get; init; }

    internal required ScalarStatistics MsUntilDisplayed { get; init; }

    internal required ScalarStatistics MsBetweenDisplayChange { get; init; }

    internal required IReadOnlyList<double> MsBetweenPresentsValues { get; init; }

    internal required IReadOnlyList<double> DisplayLatencyValues { get; init; }

    internal required IReadOnlyList<double> MsUntilDisplayedValues { get; init; }

    internal required IReadOnlyList<double> MsBetweenDisplayChangeValues { get; init; }

    internal required CapFrameXStutterResult CapFrameXStutter { get; init; }
}

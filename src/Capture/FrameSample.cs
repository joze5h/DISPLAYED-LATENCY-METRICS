using DisplayedLatencyMetrics.Interop;

namespace DisplayedLatencyMetrics.Capture;

internal readonly record struct FrameSample(
    ulong TimestampQpc,
    PmFrameType FrameType,
    PmPresentMode PresentMode,
    PmGraphicsRuntime PresentRuntime,
    int SyncInterval,
    bool AllowsTearing,
    double BetweenAppStartMs,
    double CpuBusyMs,
    double CpuWaitMs,
    double GpuBusyMs,
    double GpuTimeMs,
    double GpuWaitMs,
    double GpuLatencyMs,
    double DisplayLatencyMs,
    double UntilDisplayedMs,
    double BetweenDisplayChangeMs,
    double BetweenPresentsMs);

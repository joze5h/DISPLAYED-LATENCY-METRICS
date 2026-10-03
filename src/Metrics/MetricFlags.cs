namespace DisplayedLatencyMetrics.Metrics;

[Flags]
internal enum MetricFlags : ulong
{
    None = 0,

    PresentMode = 1UL << 0,

    MsBetweenAppStart = 1UL << 1,
    MsCPUBusy = 1UL << 2,
    MsCPUWait = 1UL << 3,

    MsGPUBusy = 1UL << 4,
    MsGPUTime = 1UL << 5,
    MsGPUWait = 1UL << 6,
    MsGPULatency = 1UL << 7,

    MsBetweenPresents = 1UL << 8,
    MsBetweenPresentsSd = 1UL << 9,
    MsBetweenPresentsRmssd = 1UL << 10,

    StutterTimePercentCapFrameX = 1UL << 11,
    StutterFramesCapFrameX = 1UL << 12,

    MsUntilDisplayed = 1UL << 13,
    MsUntilDisplayedSd = 1UL << 14,
    MsUntilDisplayedRmssd = 1UL << 15,
    MsBetweenDisplayChange = 1UL << 16,
    MsBetweenDisplayChangeSd = 1UL << 17,
    MsBetweenDisplayChangeRmssd = 1UL << 18,
    MsBetweenDisplayChangeStepwiseRelative = 1UL << 19,

    DisplayLatency = 1UL << 20,
    DisplayLatencySd = 1UL << 21,
    DisplayLatencyRmssd = 1UL << 22,

    All = PresentMode |
          MsBetweenAppStart |
          MsCPUBusy |
          MsCPUWait |
          MsGPUBusy |
          MsGPUTime |
          MsGPUWait |
          MsGPULatency |
          MsBetweenPresents |
          MsBetweenPresentsSd |
          MsBetweenPresentsRmssd |
          StutterTimePercentCapFrameX |
          StutterFramesCapFrameX |
          MsUntilDisplayed |
          MsUntilDisplayedSd |
          MsUntilDisplayedRmssd |
          MsBetweenDisplayChange |
          MsBetweenDisplayChangeSd |
          MsBetweenDisplayChangeRmssd |
          MsBetweenDisplayChangeStepwiseRelative |
          DisplayLatency |
          DisplayLatencySd |
          DisplayLatencyRmssd,
}

internal enum MetricGroup
{
    Presentation,
    Cpu,
    Render,
    Display,
    Analysis,
}

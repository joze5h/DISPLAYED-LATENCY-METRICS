# DISPLAYED-LATENCY-METRICS

Windows frame-timing overlay for Counter-Strike 2.

![Application preview](src/Assets/image.png)

## Scope

The app captures **Counter-Strike 2 only** (`cs2.exe`) through PresentMon API **2.5.1**. It calculates metrics for the selected swap chain over a rolling 1-second window.

Output is available through a native Windows overlay or, optionally, RTSS OSD.

## Metrics

The app registers 23 metrics covering:

- **Presentation:** present mode, time between presents, standard deviation and RMSSD.
- **CPU and GPU:** app-start interval, CPU busy/wait, GPU busy/time/wait/latency.
- **Display:** display latency, time until displayed, intervals between display changes, standard deviation, RMSSD and stepwise-relative timing.
- **Stutter:** CapFrameX-style stutter-frame count and stutter-time percentage.

## Requirements

- Windows x64 and the .NET 10 SDK to build.
- PresentMon API 2.5.1 installed to capture frames.
- RTSS installed and running only if using RTSS OSD output.

The project targets `net10.0-windows` and publishes as a self-contained, single-file Native AOT application for `win-x64`.

## Build

Run from the repository root:

```powershell
dotnet restore .\src\DISPLAYED-LATENCY-METRICS.csproj
dotnet build .\src\DISPLAYED-LATENCY-METRICS.csproj -c Release
dotnet publish .\src\DISPLAYED-LATENCY-METRICS.csproj -c Release -r win-x64
```

## Layout

```text
DISPLAYED-LATENCY-METRICS.slnx
src/
├── DISPLAYED-LATENCY-METRICS.csproj
├── App/
├── Assets/
├── Capture/
├── Infrastructure/
├── Interop/
├── Metrics/
└── Overlay/
```

Frame data flows from PresentMon through the decoder and capture pipeline to the metric engine, then to the selected overlay output.

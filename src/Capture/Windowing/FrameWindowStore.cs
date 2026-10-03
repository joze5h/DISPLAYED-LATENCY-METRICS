using System.Diagnostics;

namespace DisplayedLatencyMetrics.Capture.Windowing;

internal sealed class FrameWindowStore
{
    private readonly Dictionary<ulong, SwapChainWindow> _windows = new();
    private readonly ulong _windowQpcTicks;
    private bool _captureActive;

    internal FrameWindowStore(double windowMilliseconds)
    {
        double ticks = Stopwatch.Frequency * Math.Max(100.0, windowMilliseconds) / 1000.0;
        _windowQpcTicks = checked((ulong)Math.Round(ticks, MidpointRounding.AwayFromZero));
    }

    internal void Reset()
    {
        _windows.Clear();
        _captureActive = false;
    }

    internal void SetCaptureActive(bool active)
    {
        if (_captureActive == active)
        {
            return;
        }

        _captureActive = active;
        foreach (SwapChainWindow window in _windows.Values)
        {
            window.ClearSamples();
        }
    }

    internal void Add(ulong swapChain, FrameSample sample)
    {
        if (!_captureActive)
        {
            return;
        }

        if (!_windows.TryGetValue(swapChain, out SwapChainWindow? window))
        {
            window = new SwapChainWindow(swapChain);
            _windows.Add(swapChain, window);
        }

        window.Add(sample);
        Prune(window);
    }

    internal IReadOnlyCollection<SwapChainWindow> GetActiveWindows()
    {
        foreach (SwapChainWindow window in _windows.Values)
        {
            Prune(window);
        }

        return _windows.Values;
    }

    private void Prune(SwapChainWindow window)
    {
        ulong now = unchecked((ulong)Stopwatch.GetTimestamp());
        ulong oldestAllowed = now > _windowQpcTicks ? now - _windowQpcTicks : 0;
        window.Prune(oldestAllowed);
    }
}

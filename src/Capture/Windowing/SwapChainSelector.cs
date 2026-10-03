using DisplayedLatencyMetrics.Metrics;

namespace DisplayedLatencyMetrics.Capture.Windowing;

internal static class SwapChainSelector
{
    internal static SwapChainWindow? Select(IReadOnlyCollection<SwapChainWindow> windows)
    {
        SwapChainWindow? best = null;
        int bestDisplayed = -1;
        int bestPresented = -1;
        int bestTotal = -1;

        foreach (SwapChainWindow window in windows)
        {
            int frameToDisplayLatency = 0;
            int presentToDisplayLatency = 0;
            int displayedIntervals = 0;
            int presented = 0;
            int total = 0;

            foreach (FrameSample sample in window.Samples)
            {
                total++;
                if (StatisticsMath.IsNonNegativeFinite(sample.DisplayLatencyMs))
                {
                    frameToDisplayLatency++;
                }
                if (StatisticsMath.IsNonNegativeFinite(sample.UntilDisplayedMs))
                {
                    presentToDisplayLatency++;
                }
                if (StatisticsMath.IsPositiveFinite(sample.BetweenDisplayChangeMs))
                {
                    displayedIntervals++;
                }
                if (StatisticsMath.IsPositiveFinite(sample.BetweenPresentsMs))
                {
                    presented++;
                }
            }

            if (total == 0)
            {
                continue;
            }

            int displayed = Math.Max(
                frameToDisplayLatency,
                Math.Max(presentToDisplayLatency, displayedIntervals));
            if (displayed > bestDisplayed ||
                (displayed == bestDisplayed && presented > bestPresented) ||
                (displayed == bestDisplayed && presented == bestPresented && total > bestTotal) ||
                (displayed == bestDisplayed && presented == bestPresented && total == bestTotal &&
                 (best is null || window.Id < best.Id)))
            {
                best = window;
                bestDisplayed = displayed;
                bestPresented = presented;
                bestTotal = total;
            }
        }

        return best;
    }
}

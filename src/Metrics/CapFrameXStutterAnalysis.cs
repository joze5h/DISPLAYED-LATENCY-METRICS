namespace DisplayedLatencyMetrics.Metrics;

internal readonly record struct CapFrameXStutterResult(
    double TimePercentage,
    int StutterFrames,
    int TotalFrames,
    int MovingAverageSampleSize)
{
    internal static CapFrameXStutterResult Unavailable =>
        new(double.NaN, 0, 0, 0);
}

internal static class CapFrameXStutterAnalysis
{
    internal const double StutteringFactor = 2.5;

    internal static CapFrameXStutterResult Analyze(IReadOnlyList<double> frametimes)
    {
        int count = frametimes.Count;
        if (count == 0)
        {
            return CapFrameXStutterResult.Unavailable;
        }

        double totalTime = 0.0;
        for (int index = 0; index < count; index++)
        {
            double value = frametimes[index];
            if (!StatisticsMath.IsPositiveFinite(value))
            {
                return CapFrameXStutterResult.Unavailable;
            }

            totalTime += value;
        }

        if (!StatisticsMath.IsPositiveFinite(totalTime))
        {
            return CapFrameXStutterResult.Unavailable;
        }

        double mean = totalTime / count;
        int sampleSize = Convert.ToInt32(Math.Sqrt(mean) * 10.0);
        double stutterTime = 0.0;
        int stutterFrames = 0;

        for (int index = 0; index < count; index++)
        {
            double baseline = MovingAverageAt(frametimes, index, sampleSize);
            if (frametimes[index] > StutteringFactor * baseline)
            {
                stutterTime += frametimes[index];
                stutterFrames++;
            }
        }

        return new CapFrameXStutterResult(
            100.0 * stutterTime / totalTime,
            stutterFrames,
            count,
            sampleSize);
    }

    private static double MovingAverageAt(
        IReadOnlyList<double> sequence,
        int index,
        int sampleSize)
    {
        int localIndex = index;
        double localSum = 0.0;
        int localCount = 0;

        while (localIndex >= 0)
        {
            double value = sequence[localIndex];
            if (localIndex > 0 && value > sequence[localIndex - 1] * 3.0)
            {
                localSum += sequence[localIndex - 1];
            }
            else
            {
                localSum += value;
            }

            localCount++;
            if (localCount >= sampleSize)
            {
                break;
            }

            localIndex--;
        }

        return localSum / localCount;
    }
}

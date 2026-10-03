namespace DisplayedLatencyMetrics.Metrics;

internal static class StatisticsMath
{

    internal static double Rmssd(
        IReadOnlyList<double> values,
        bool allowZero,
        out int pairs)
    {
        pairs = 0;
        if (values.Count < 2)
        {
            return double.NaN;
        }

        double sumSquares = 0.0;
        for (int index = 1; index < values.Count; index++)
        {
            double previous = values[index - 1];
            double current = values[index];
            bool valid = allowZero
                ? IsNonNegativeFinite(previous) && IsNonNegativeFinite(current)
                : IsPositiveFinite(previous) && IsPositiveFinite(current);
            if (!valid)
            {
                continue;
            }

            double difference = current - previous;
            sumSquares += difference * difference;
            pairs++;
        }

        return pairs > 0 ? Math.Sqrt(sumSquares / pairs) : double.NaN;
    }

    internal static double StepwiseRelative(
        IReadOnlyList<double> values,
        bool allowZero,
        out int pairs)
    {
        pairs = 0;
        if (values.Count < 2)
        {
            return double.NaN;
        }

        double sumRelativeChange = 0.0;
        for (int index = 1; index < values.Count; index++)
        {
            double previous = values[index - 1];
            double current = values[index];
            bool valid = allowZero
                ? IsNonNegativeFinite(previous) && IsNonNegativeFinite(current)
                : IsPositiveFinite(previous) && IsPositiveFinite(current);
            if (!valid || previous <= 0.0)
            {
                continue;
            }

            sumRelativeChange += Math.Abs(current - previous) / previous;
            pairs++;
        }

        return pairs > 0
            ? 100.0 * sumRelativeChange / pairs
            : double.NaN;
    }

    internal static bool IsPositiveFinite(double value) =>
        double.IsFinite(value) && value > 0.0;

    internal static bool IsNonNegativeFinite(double value) =>
        double.IsFinite(value) && value >= 0.0;
}

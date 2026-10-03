using System.Globalization;

namespace DisplayedLatencyMetrics.Metrics;

internal static class MetricValueFormatter
{
    internal static string Number(double value, int decimals, string suffix)
    {
        if (!double.IsFinite(value))
        {
            return "N/A";
        }

        return value.ToString(
                   "F" + decimals.ToString(CultureInfo.InvariantCulture),
                   CultureInfo.InvariantCulture) + suffix;
    }
}

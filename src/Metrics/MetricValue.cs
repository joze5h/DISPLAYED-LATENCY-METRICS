namespace DisplayedLatencyMetrics.Metrics;

internal readonly record struct MetricValue(
    double Number,
    int SampleCount = 0,
    int EventCount = 0,
    string? Text = null)
{
    internal static MetricValue Unavailable => new(double.NaN);

    internal static MetricValue Numeric(double number, int sampleCount = 0) =>
        new(number, sampleCount);

    internal static MetricValue Events(double percentage, int events, int samples) =>
        new(percentage, samples, events);

    internal static MetricValue Textual(string text) =>
        new(double.NaN, Text: text);

    internal bool HasNumber => double.IsFinite(Number);

    internal bool HasText => !string.IsNullOrEmpty(Text);
}

internal readonly record struct MetricFormatOptions(int DecimalPlaces, bool ShowUnits);

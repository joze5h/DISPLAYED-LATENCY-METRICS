namespace DisplayedLatencyMetrics.Metrics;

internal interface IMetric
{
    MetricFlags Flag { get; }

    string Name { get; }

    MetricGroup Group { get; }

    MetricValue Calculate(MetricContext context);

    string Format(MetricValue value, MetricFormatOptions options);
}

internal abstract class NumericMetric : IMetric
{
    public abstract MetricFlags Flag { get; }

    public abstract string Name { get; }

    public abstract MetricGroup Group { get; }

    protected virtual string Unit => string.Empty;

    protected virtual int MaximumDecimalPlaces => int.MaxValue;

    public abstract MetricValue Calculate(MetricContext context);

    public virtual string Format(MetricValue value, MetricFormatOptions options)
    {
        if (!value.HasNumber)
        {
            return "N/A";
        }

        int decimals = Math.Min(options.DecimalPlaces, MaximumDecimalPlaces);
        string suffix = options.ShowUnits ? Unit : string.Empty;
        return MetricValueFormatter.Number(value.Number, decimals, suffix);
    }
}

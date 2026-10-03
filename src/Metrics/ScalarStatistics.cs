namespace DisplayedLatencyMetrics.Metrics;

internal readonly record struct ScalarStatistics(
    int Count,
    double Average,
    double PopulationSd)
{
    internal static ScalarStatistics Empty => new(0, double.NaN, double.NaN);
}

internal struct RunningStatistics
{
    private int _count;
    private double _mean;
    private double _m2;

    internal void Add(double value)
    {
        _count++;
        double delta = value - _mean;
        _mean += delta / _count;
        double delta2 = value - _mean;
        _m2 += delta * delta2;
    }

    internal readonly ScalarStatistics Snapshot() => _count == 0
        ? ScalarStatistics.Empty
        : new ScalarStatistics(
            _count,
            _mean,
            Math.Sqrt(Math.Max(0.0, _m2 / _count)));
}

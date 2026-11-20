namespace SignalClean.Core.Processing;

/// <summary>Правила виявлення аномалій у потоці вимірювань.</summary>
public static class AnomalyDetector
{
    /// <summary>
    /// Чи є значення сплеском відносно попередніх (каузальний варіант фільтра Гампеля).
    /// <paramref name="minAbsoluteDeviation"/> захищає від хибних спрацювань, коли сигнал майже постійний (MAD ≈ 0).
    /// </summary>
    public static bool IsSpike(IReadOnlyList<double> previous, double value, double nSigma, double minAbsoluteDeviation, out double expected)
    {
        ArgumentNullException.ThrowIfNull(previous);
        expected = value;
        if (previous.Count < 3)
        {
            return false;
        }

        expected = Filters.Median(previous);
        double sigma = Filters.MadToSigma * Filters.MedianAbsoluteDeviation(previous);
        double threshold = Math.Max(nSigma * sigma, minAbsoluteDeviation);
        return Math.Abs(value - expected) > threshold;
    }

    /// <summary>Довжина серії однакових значень у кінці ряду («залиплий» датчик).</summary>
    public static int TrailingRunLength(IReadOnlyList<double> values, double tolerance = 1e-9)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            return 0;
        }

        double last = values[^1];
        int run = 1;
        for (int i = values.Count - 2; i >= 0 && Math.Abs(values[i] - last) <= tolerance; i--)
        {
            run++;
        }

        return run;
    }

    /// <summary>
    /// Чи є розрив у даних: інтервал до нової точки більший за <paramref name="factor"/> медіанних інтервалів історії.
    /// </summary>
    public static bool IsGap(IReadOnlyList<DateTimeOffset> previousTimestamps, DateTimeOffset timestamp, double factor, out TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(previousTimestamps);
        interval = TimeSpan.Zero;
        if (previousTimestamps.Count < 3)
        {
            return false;
        }

        var intervals = new List<double>(previousTimestamps.Count - 1);
        for (int i = 1; i < previousTimestamps.Count; i++)
        {
            intervals.Add((previousTimestamps[i] - previousTimestamps[i - 1]).TotalSeconds);
        }

        double typical = Filters.Median(intervals);
        interval = timestamp - previousTimestamps[^1];
        return typical > 0 && interval.TotalSeconds > factor * typical;
    }
}

using SignalClean.Core.Validation;

namespace SignalClean.Core.Processing;

/// <summary>Параметри конвеєра оброблення.</summary>
public sealed record ProcessingOptions
{
    /// <summary>Скільки попередніх коректних значень використовується для пошуку сплесків.</summary>
    public int Window { get; init; } = 15;

    /// <summary>Поріг сплеску в оцінках σ.</summary>
    public double SpikeSigma { get; init; } = 4.0;

    /// <summary>Мінімальне відхилення для сплеску як частка діапазону датчика.</summary>
    public double MinSpikeFraction { get; init; } = 0.02;

    /// <summary>Скільки однакових значень поспіль вважається «залипанням».</summary>
    public int StuckRun { get; init; } = 6;

    /// <summary>У скільки разів інтервал має перевищити типовий, щоб вважатися розривом.</summary>
    public double GapFactor { get; init; } = 3.0;

    public static ProcessingOptions Default { get; } = new();
}

/// <summary>
/// Конвеєр оброблення вимірювань одного датчика:
/// валідація → пошук розриву → пошук сплеску (заміна медіаною) → пошук «залипання».
/// Клас не має стану: історія передається ззовні, тому його легко тестувати.
/// </summary>
public sealed class SignalProcessor
{
    private readonly ProcessingOptions _options;

    public SignalProcessor(ProcessingOptions? options = null)
    {
        _options = options ?? ProcessingOptions.Default;
        ArgumentOutOfRangeException.ThrowIfLessThan(_options.Window, 3);
        ArgumentOutOfRangeException.ThrowIfLessThan(_options.StuckRun, 2);
    }

    public IReadOnlyList<ProcessedPoint> Process(
        IEnumerable<HistoryPoint> history,
        IEnumerable<RawPoint> points,
        double minValid,
        double maxValid,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(points);

        var timestamps = new List<DateTimeOffset>();
        var cleanValues = new List<double>();
        var rawRun = new List<double>();
        foreach (var h in history.OrderBy(h => h.Timestamp))
        {
            timestamps.Add(h.Timestamp);
            if (h.CleanValue is double v)
            {
                cleanValues.Add(v);
                rawRun.Add(v);
            }
        }

        double minSpike = (maxValid - minValid) * _options.MinSpikeFraction;
        var result = new List<ProcessedPoint>();

        foreach (var p in points.OrderBy(p => p.Timestamp))
        {
            var anomalies = new List<DetectedAnomaly>();
            var validation = ReadingValidator.Validate(p.Value, p.Timestamp, minValid, maxValid, now);

            if (validation.Status != ReadingStatus.Invalid
                && AnomalyDetector.IsGap(Tail(timestamps, _options.Window), p.Timestamp, _options.GapFactor, out var gap))
            {
                anomalies.Add(new DetectedAnomaly(p.Timestamp, AnomalyKind.Gap, null, $"Розрив у даних: {gap.TotalSeconds:F0} с"));
            }

            if (!validation.IsUsable)
            {
                if (validation.Anomaly is AnomalyKind kind)
                {
                    anomalies.Add(new DetectedAnomaly(p.Timestamp, kind, Finite(p.Value), validation.Error ?? kind.ToString()));
                }

                if (validation.Status == ReadingStatus.Missing)
                {
                    timestamps.Add(p.Timestamp);
                }

                result.Add(new ProcessedPoint(p.Timestamp, p.Value, null, validation.Status, anomalies));
                continue;
            }

            double value = p.Value!.Value;
            double clean = value;
            var status = ReadingStatus.Valid;

            if (AnomalyDetector.IsSpike(Tail(cleanValues, _options.Window), value, _options.SpikeSigma, minSpike, out double expected))
            {
                clean = expected;
                status = ReadingStatus.Corrected;
                anomalies.Add(new DetectedAnomaly(p.Timestamp, AnomalyKind.Spike, value, $"Сплеск: {value:G6}, очікувалось ≈ {expected:G6}"));
            }

            rawRun.Add(value);
            if (AnomalyDetector.TrailingRunLength(Tail(rawRun, _options.StuckRun)) == _options.StuckRun
                && AnomalyDetector.TrailingRunLength(Tail(rawRun, _options.StuckRun + 1)) == _options.StuckRun)
            {
                // Фіксуємо лише момент, коли серія досягла порогу, а не кожну наступну точку.
                anomalies.Add(new DetectedAnomaly(p.Timestamp, AnomalyKind.Stuck, value, $"Значення {value:G6} не змінюється {_options.StuckRun} вимірювань поспіль"));
            }

            timestamps.Add(p.Timestamp);
            cleanValues.Add(clean);
            result.Add(new ProcessedPoint(p.Timestamp, value, clean, status, anomalies));
        }

        return result;
    }

    private static double? Finite(double? value) =>
        value is double v && double.IsFinite(v) ? v : null;

    private static List<T> Tail<T>(List<T> list, int count) =>
        list.Count <= count ? list : list.GetRange(list.Count - count, count);
}

namespace SignalClean.Core;

/// <summary>Стан вимірювання після оброблення.</summary>
public enum ReadingStatus
{
    /// <summary>Значення коректне і не змінювалося.</summary>
    Valid,

    /// <summary>Значення було викидом і замінене очищеним.</summary>
    Corrected,

    /// <summary>Значення некоректне (NaN, поза діапазоном, неправильний час).</summary>
    Invalid,

    /// <summary>Значення відсутнє (пропуск).</summary>
    Missing,
}

/// <summary>Тип виявленої аномалії.</summary>
public enum AnomalyKind
{
    Spike,
    OutOfRange,
    NotANumber,
    Stuck,
    Gap,
    InvalidTimestamp,
}

/// <summary>Точка історії датчика, на основі якої обробляються нові значення.</summary>
public readonly record struct HistoryPoint(DateTimeOffset Timestamp, double? CleanValue);

/// <summary>Вхідна точка для оброблення.</summary>
public readonly record struct RawPoint(DateTimeOffset Timestamp, double? Value);

/// <summary>Аномалія, знайдена під час оброблення.</summary>
public sealed record DetectedAnomaly(DateTimeOffset Timestamp, AnomalyKind Kind, double? Value, string Details);

/// <summary>Результат оброблення однієї точки.</summary>
public sealed record ProcessedPoint(
    DateTimeOffset Timestamp,
    double? RawValue,
    double? CleanValue,
    ReadingStatus Status,
    IReadOnlyList<DetectedAnomaly> Anomalies);
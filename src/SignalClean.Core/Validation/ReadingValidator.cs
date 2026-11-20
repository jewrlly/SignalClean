namespace SignalClean.Core.Validation;

/// <summary>Результат перевірки одного значення.</summary>
public sealed record ValidationOutcome(ReadingStatus Status, AnomalyKind? Anomaly, string? Error)
{
    public bool IsUsable => Status == ReadingStatus.Valid;

    public static ValidationOutcome Ok { get; } = new(ReadingStatus.Valid, null, null);
}

/// <summary>
/// Перевіряє сире вимірювання: пропуск, NaN/нескінченність, фізичний діапазон датчика, коректність часу.
/// </summary>
public static class ReadingValidator
{
    /// <summary>Наскільки час вимірювання може бути «в майбутньому» (розсинхронізація годинників).</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>Найраніша допустима дата вимірювання.</summary>
    public static readonly DateTimeOffset MinTimestamp = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static ValidationOutcome Validate(double? value, DateTimeOffset timestamp, double minValid, double maxValid, DateTimeOffset now)
    {
        if (minValid >= maxValid)
        {
            throw new ArgumentException("Нижня межа діапазону має бути меншою за верхню.", nameof(minValid));
        }

        if (timestamp > now + MaxClockSkew || timestamp < MinTimestamp)
        {
            return new ValidationOutcome(ReadingStatus.Invalid, AnomalyKind.InvalidTimestamp, $"Некоректний час вимірювання: {timestamp:O}");
        }

        if (value is null)
        {
            return new ValidationOutcome(ReadingStatus.Missing, null, null);
        }

        double v = value.Value;
        if (double.IsNaN(v) || double.IsInfinity(v))
        {
            return new ValidationOutcome(ReadingStatus.Invalid, AnomalyKind.NotANumber, "Значення не є скінченним числом");
        }

        if (v < minValid || v > maxValid)
        {
            return new ValidationOutcome(ReadingStatus.Invalid, AnomalyKind.OutOfRange, $"Значення {v} поза діапазоном [{minValid}; {maxValid}]");
        }

        return ValidationOutcome.Ok;
    }
}

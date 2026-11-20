namespace SignalClean.Simulator;

/// <summary>Ймовірності дефектів, які симулятор підмішує в сигнал.</summary>
public sealed record DefectProfile
{
    public double SpikeProbability { get; init; } = 0.03;

    public double MissingProbability { get; init; } = 0.02;

    public double NaNProbability { get; init; } = 0.01;

    public double OutOfRangeProbability { get; init; } = 0.01;

    public double StuckStartProbability { get; init; } = 0.005;

    public int StuckLength { get; init; } = 8;

    public double DropProbability { get; init; } = 0.01;

    public static DefectProfile None { get; } = new()
    {
        SpikeProbability = 0,
        MissingProbability = 0,
        NaNProbability = 0,
        OutOfRangeProbability = 0,
        StuckStartProbability = 0,
        DropProbability = 0,
    };
}

/// <summary>
/// Модель одного датчика: синусоїда (добовий цикл) + шум + випадкові дефекти.
/// </summary>
public sealed class SensorModel(string code, double baseline, double amplitude, double noise, double periodSeconds, DefectProfile defects, Random random)
{
    /// <summary>Типовий «код помилки», який повертають несправні датчики.</summary>
    public const double ErrorCode = -999;

    private int _stuckLeft;
    private double _stuckValue;

    public string Code { get; } = code;

    /// <summary>
    /// Повертає значення для моменту <paramref name="t"/>. Якщо результат false — пакет «загубився» і не надсилається.
    /// </summary>
    public bool TryNext(DateTimeOffset t, out double? value)
    {
        value = null;
        if (random.NextDouble() < defects.DropProbability)
        {
            return false;
        }

        double seconds = t.ToUnixTimeMilliseconds() / 1000.0;
        double clean = baseline + (amplitude * Math.Sin(2 * Math.PI * seconds / periodSeconds)) + (noise * Gaussian());

        if (_stuckLeft > 0)
        {
            _stuckLeft--;
            value = _stuckValue;
            return true;
        }

        double roll = random.NextDouble();
        double edge = defects.MissingProbability;
        if (roll < edge)
        {
            value = null;
            return true;
        }

        if (roll < (edge += defects.NaNProbability))
        {
            value = double.NaN;
            return true;
        }

        if (roll < (edge += defects.OutOfRangeProbability))
        {
            value = ErrorCode;
            return true;
        }

        if (roll < (edge += defects.SpikeProbability))
        {
            double sign = random.Next(2) == 0 ? -1 : 1;
            value = Math.Round(clean + (sign * Math.Max(amplitude, 1) * 4), 2);
            return true;
        }

        if (roll < edge + defects.StuckStartProbability)
        {
            _stuckValue = Math.Round(clean, 2);
            _stuckLeft = defects.StuckLength - 1;
            value = _stuckValue;
            return true;
        }

        value = Math.Round(clean, 2);
        return true;
    }

    // Нормальний розподіл методом Бокса — Мюллера.
    private double Gaussian()
    {
        double u1 = 1.0 - random.NextDouble();
        double u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}

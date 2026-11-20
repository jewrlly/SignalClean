namespace SignalClean.Core.Processing;

/// <summary>Цифрові фільтри для очищення сигналу.</summary>
public static class Filters
{
    /// <summary>Коефіцієнт, що переводить MAD в оцінку стандартного відхилення для нормального розподілу.</summary>
    public const double MadToSigma = 1.4826;

    /// <summary>Медіана вибірки.</summary>
    public static double Median(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("Вибірка порожня.", nameof(values));
        }

        var sorted = values.ToArray();
        Array.Sort(sorted);
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    /// <summary>Медіанне абсолютне відхилення (MAD): стійка до викидів міра розкиду.</summary>
    public static double MedianAbsoluteDeviation(IReadOnlyList<double> values)
    {
        double median = Median(values);
        var deviations = values.Select(v => Math.Abs(v - median)).ToArray();
        return Median(deviations);
    }

    /// <summary>Ковзне середнє з вікном <paramref name="window"/> (вікно на початку ряду коротше).</summary>
    public static double[] MovingAverage(IReadOnlyList<double> values, int window)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfLessThan(window, 1);

        var result = new double[values.Count];
        double sum = 0;
        for (int i = 0; i < values.Count; i++)
        {
            sum += values[i];
            if (i >= window)
            {
                sum -= values[i - window];
            }

            result[i] = sum / Math.Min(i + 1, window);
        }

        return result;
    }

    /// <summary>
    /// Фільтр Гампеля: точка, що відхиляється від медіани вікна більше ніж на nSigma·σ (σ = 1.4826·MAD),
    /// вважається викидом і замінюється медіаною.
    /// </summary>
    public static (double[] Cleaned, bool[] IsOutlier) Hampel(IReadOnlyList<double> values, int halfWindow = 3, double nSigma = 3.0)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfLessThan(halfWindow, 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nSigma);

        var cleaned = values.ToArray();
        var outliers = new bool[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            int from = Math.Max(0, i - halfWindow);
            int to = Math.Min(values.Count - 1, i + halfWindow);
            var window = new double[to - from + 1];
            for (int j = from; j <= to; j++)
            {
                window[j - from] = values[j];
            }

            double median = Median(window);
            double sigma = MadToSigma * MedianAbsoluteDeviation(window);
            if (sigma > 0 && Math.Abs(values[i] - median) > nSigma * sigma)
            {
                cleaned[i] = median;
                outliers[i] = true;
            }
        }

        return (cleaned, outliers);
    }
}

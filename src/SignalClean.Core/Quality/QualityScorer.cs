namespace SignalClean.Core.Quality;

/// <summary>
/// Обчислює індекс якості даних (Data Quality Score, 0–100) за трьома складовими:
/// повнота (немає пропусків), достовірність (значення коректні) та частка аномалій.
/// </summary>
public static class QualityScorer
{
    public const double CompletenessWeight = 0.35;
    public const double ValidityWeight = 0.35;
    public const double AnomalyWeight = 0.30;

    /// <summary>Частка аномалій, за якої відповідна складова стає нульовою (20 %).</summary>
    public const double AnomalyRateCeiling = 0.20;

    public static QualityReportDto Compute(string sensorCode, IReadOnlyCollection<ReadingStatus> statuses, int anomalyCount)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentOutOfRangeException.ThrowIfNegative(anomalyCount);

        int total = statuses.Count;
        if (total == 0)
        {
            return new QualityReportDto(sensorCode, 0, "N/A", 0, 0, 0, 0, anomalyCount);
        }

        int missing = statuses.Count(s => s == ReadingStatus.Missing);
        int present = total - missing;
        int usable = statuses.Count(s => s is ReadingStatus.Valid or ReadingStatus.Corrected);

        double completeness = (double)present / total;
        double validity = present == 0 ? 0 : (double)usable / present;
        double anomalyRate = Math.Min(1.0, (double)anomalyCount / total);
        double anomalyComponent = 1.0 - Math.Min(1.0, anomalyRate / AnomalyRateCeiling);

        double score = 100.0 * ((CompletenessWeight * completeness) + (ValidityWeight * validity) + (AnomalyWeight * anomalyComponent));
        score = Math.Round(score, 1);

        return new QualityReportDto(
            sensorCode,
            score,
            GradeFor(score),
            Math.Round(completeness, 4),
            Math.Round(validity, 4),
            Math.Round(anomalyRate, 4),
            total,
            anomalyCount);
    }

    public static string GradeFor(double score) => score switch
    {
        >= 90 => "A",
        >= 75 => "B",
        >= 50 => "C",
        _ => "D",
    };
}

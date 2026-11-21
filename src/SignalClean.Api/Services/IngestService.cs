using Microsoft.EntityFrameworkCore;

using SignalClean.Core;
using SignalClean.Core.Processing;
using SignalClean.Data;

namespace SignalClean.Api.Services;

/// <summary>
/// Приймає пакет вимірювань: обробляє кожен датчик конвеєром <see cref="SignalProcessor"/>
/// і зберігає все в одній транзакції (або весь пакет, або нічого).
/// </summary>
public sealed partial class IngestService(SignalCleanDbContext db, TimeProvider clock, ILogger<IngestService> logger)
{
    public const int MaxBatchSize = 1000;

    /// <summary>Скільки останніх вимірювань датчика беремо як історію для пошуку сплесків і розривів.</summary>
    public const int HistorySize = 30;

    private static readonly SignalProcessor Processor = new();

    public async Task<IngestResult> IngestAsync(IReadOnlyList<ReadingInput?> readings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var errors = new List<string>();
        int accepted = 0;
        int rejected = 0;
        int anomalies = 0;

        var withCode = new List<ReadingInput>(readings.Count);
        foreach (var r in readings)
        {
            if (r is null || string.IsNullOrWhiteSpace(r.SensorCode))
            {
                rejected++;
                AddError(errors, "Вимірювання без коду датчика відхилено");
            }
            else
            {
                withCode.Add(r);
            }
        }

        var codes = withCode.Select(r => NormalizeCode(r.SensorCode)).Distinct().ToList();
        var sensors = await db.Sensors
            .Include(s => s.SensorType)
            .Where(s => codes.Contains(s.Code))
            .ToDictionaryAsync(s => s.Code, ct);

        var now = clock.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        foreach (var group in withCode.GroupBy(r => NormalizeCode(r.SensorCode)))
        {
            if (!sensors.TryGetValue(group.Key, out var sensor))
            {
                rejected += group.Count();
                AddError(errors, $"Невідомий датчик: {group.Key}");
                continue;
            }

            var history = await db.Readings
                .AsNoTracking()
                .Where(r => r.SensorId == sensor.Id)
                .OrderByDescending(r => r.TimestampUtc)
                .Take(HistorySize)
                .Select(r => new { r.TimestampUtc, r.CleanValue })
                .ToListAsync(ct);

            var processed = Processor.Process(
                history.Select(h => new HistoryPoint(ToOffset(h.TimestampUtc), h.CleanValue)),
                group.Select(r => new RawPoint(r.Timestamp, r.Value)),
                sensor.SensorType.MinValid,
                sensor.SensorType.MaxValid,
                now);

            foreach (var p in processed)
            {
                db.Readings.Add(new Reading
                {
                    SensorId = sensor.Id,
                    TimestampUtc = p.Timestamp.UtcDateTime,
                    RawValue = Finite(p.RawValue),
                    CleanValue = p.CleanValue,
                    Status = p.Status,
                });

                foreach (var a in p.Anomalies)
                {
                    db.Anomalies.Add(new Anomaly
                    {
                        SensorId = sensor.Id,
                        TimestampUtc = a.Timestamp.UtcDateTime,
                        Kind = a.Kind,
                        Value = Finite(a.Value),
                        Details = a.Details.Length > 300 ? a.Details[..300] : a.Details,
                    });
                }

                anomalies += p.Anomalies.Count;
                if (p.Status == ReadingStatus.Invalid)
                {
                    rejected++;
                }
                else
                {
                    accepted++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        LogIngested(logger, accepted, rejected, anomalies);
        return new IngestResult(accepted, rejected, anomalies, errors);
    }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    internal static DateTimeOffset ToOffset(DateTime utc) =>
        new(DateTime.SpecifyKind(utc, DateTimeKind.Utc));

    private static double? Finite(double? value) =>
        value is double v && double.IsFinite(v) ? v : null;

    private static void AddError(List<string> errors, string message)
    {
        // Не роздуваємо відповідь: однакові помилки не дублюємо, максимум 20.
        if (errors.Count < 20 && !errors.Contains(message))
        {
            errors.Add(message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Пакет оброблено: прийнято {Accepted}, відхилено {Rejected}, аномалій {Anomalies}")]
    private static partial void LogIngested(ILogger logger, int accepted, int rejected, int anomalies);
}
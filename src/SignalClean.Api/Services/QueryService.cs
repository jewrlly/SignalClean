using Microsoft.EntityFrameworkCore;

using SignalClean.Core;
using SignalClean.Core.Quality;
using SignalClean.Data;

namespace SignalClean.Api.Services;

/// <summary>Запити на читання: датчики, вимірювання, аномалії, якість даних.</summary>
public sealed class QueryService(SignalCleanDbContext db)
{
    /// <summary>За скількома останніми вимірюваннями рахується індекс якості.</summary>
    public const int DefaultQualityWindow = 200;

    public const int MaxLimit = 1000;

    public async Task<List<SensorDto>> GetSensorsAsync(CancellationToken ct)
    {
        var sensors = await db.Sensors
            .AsNoTracking()
            .Include(s => s.SensorType)
            .OrderBy(s => s.Code)
            .ToListAsync(ct);

        var result = new List<SensorDto>(sensors.Count);
        foreach (var s in sensors)
        {
            var quality = await ComputeQualityAsync(s, DefaultQualityWindow, ct);
            result.Add(ToDto(s, quality));
        }

        return result;
    }

    public async Task<SensorDto?> GetSensorAsync(string code, CancellationToken ct)
    {
        var sensor = await FindSensorAsync(code, ct);
        if (sensor is null)
        {
            return null;
        }

        var quality = await ComputeQualityAsync(sensor, DefaultQualityWindow, ct);
        return ToDto(sensor, quality);
    }

    public async Task<List<ReadingDto>?> GetReadingsAsync(string code, int limit, CancellationToken ct)
    {
        var sensor = await FindSensorAsync(code, ct);
        if (sensor is null)
        {
            return null;
        }

        var rows = await db.Readings
            .AsNoTracking()
            .Where(r => r.SensorId == sensor.Id)
            .OrderByDescending(r => r.TimestampUtc)
            .ThenByDescending(r => r.Id)
            .Take(ClampLimit(limit))
            .ToListAsync(ct);

        rows.Reverse(); // для графіка потрібен хронологічний порядок
        return rows
            .Select(r => new ReadingDto(IngestService.ToOffset(r.TimestampUtc), r.RawValue, r.CleanValue, r.Status.ToString()))
            .ToList();
    }

    public async Task<List<AnomalyDto>?> GetAnomaliesAsync(string code, int limit, CancellationToken ct)
    {
        var sensor = await FindSensorAsync(code, ct);
        if (sensor is null)
        {
            return null;
        }

        var rows = await db.Anomalies
            .AsNoTracking()
            .Where(a => a.SensorId == sensor.Id)
            .OrderByDescending(a => a.TimestampUtc)
            .ThenByDescending(a => a.Id)
            .Take(ClampLimit(limit))
            .ToListAsync(ct);

        return rows
            .Select(a => new AnomalyDto(a.Id, sensor.Code, IngestService.ToOffset(a.TimestampUtc), a.Kind.ToString(), a.Value, a.Details))
            .ToList();
    }

    public async Task<QualityReportDto?> GetQualityAsync(string code, int window, CancellationToken ct)
    {
        var sensor = await FindSensorAsync(code, ct);
        return sensor is null ? null : await ComputeQualityAsync(sensor, ClampLimit(window), ct);
    }

    public static int ClampLimit(int limit) => Math.Clamp(limit, 1, MaxLimit);

    private Task<Sensor?> FindSensorAsync(string code, CancellationToken ct)
    {
        string normalized = IngestService.NormalizeCode(code);
        return db.Sensors
            .AsNoTracking()
            .Include(s => s.SensorType)
            .FirstOrDefaultAsync(s => s.Code == normalized, ct);
    }

    private async Task<QualityReportDto> ComputeQualityAsync(Sensor sensor, int window, CancellationToken ct)
    {
        var last = await db.Readings
            .AsNoTracking()
            .Where(r => r.SensorId == sensor.Id)
            .OrderByDescending(r => r.TimestampUtc)
            .Take(window)
            .Select(r => new { r.Status, r.TimestampUtc })
            .ToListAsync(ct);

        if (last.Count == 0)
        {
            return QualityScorer.Compute(sensor.Code, [], 0);
        }

        var from = last.Min(r => r.TimestampUtc);
        int anomalyCount = await db.Anomalies.CountAsync(a => a.SensorId == sensor.Id && a.TimestampUtc >= from, ct);

        return QualityScorer.Compute(sensor.Code, last.Select(r => r.Status).ToList(), anomalyCount);
    }

    private static SensorDto ToDto(Sensor s, QualityReportDto quality) => new(
        s.Id,
        s.Code,
        s.Name,
        s.SensorType.Name,
        s.SensorType.Unit,
        s.Location,
        s.SensorType.MinValid,
        s.SensorType.MaxValid,
        quality.TotalReadings == 0 ? null : quality.Score,
        quality.Grade);
}
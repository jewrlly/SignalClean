using SignalClean.Core;

namespace SignalClean.Data;

// Схема БД у 3НФ:
//  SensorType (довідник типів: одиниці та фізичний діапазон залежать лише від типу)
//  Sensor     (FK → SensorType)
//  Reading    (FK → Sensor)
//  Anomaly    (FK → Sensor)
// Час зберігається як DateTime в UTC: SQLite-провайдер EF Core не вміє сортувати DateTimeOffset.

public class SensorType
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Unit { get; set; }

    public double MinValid { get; set; }

    public double MaxValid { get; set; }

    public List<Sensor> Sensors { get; set; } = [];
}

public class Sensor
{
    public int Id { get; set; }

    public required string Code { get; set; }

    public required string Name { get; set; }

    public string? Location { get; set; }

    public int SensorTypeId { get; set; }

    public SensorType SensorType { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public List<Reading> Readings { get; set; } = [];

    public List<Anomaly> Anomalies { get; set; } = [];
}

public class Reading
{
    public long Id { get; set; }

    public int SensorId { get; set; }

    public Sensor Sensor { get; set; } = null!;

    public DateTime TimestampUtc { get; set; }

    public double? RawValue { get; set; }

    public double? CleanValue { get; set; }

    public ReadingStatus Status { get; set; }
}

public class Anomaly
{
    public long Id { get; set; }

    public int SensorId { get; set; }

    public Sensor Sensor { get; set; } = null!;

    public DateTime TimestampUtc { get; set; }

    public AnomalyKind Kind { get; set; }

    public double? Value { get; set; }

    public required string Details { get; set; }
}
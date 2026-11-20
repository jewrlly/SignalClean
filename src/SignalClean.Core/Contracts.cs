namespace SignalClean.Core;

// DTO-контракти, спільні для API, симулятора та клієнта.

/// <summary>Датчик разом з поточним індексом якості даних.</summary>
public sealed record SensorDto(
    int Id,
    string Code,
    string Name,
    string Type,
    string Unit,
    string? Location,
    double MinValid,
    double MaxValid,
    double? QualityScore,
    string Grade);

/// <summary>Запит на створення нового датчика.</summary>
public sealed record CreateSensorRequest(string Code, string Name, string TypeName, string? Location);

/// <summary>Одне вимірювання, яке надсилає сенсорний вузол. Value = null означає пропуск.</summary>
public sealed record ReadingInput(string SensorCode, DateTimeOffset Timestamp, double? Value);

/// <summary>Пакет вимірювань.</summary>
public sealed record IngestRequest(IReadOnlyList<ReadingInput>? Readings);

/// <summary>Результат прийому пакета.</summary>
public sealed record IngestResult(int Accepted, int Rejected, int Anomalies, IReadOnlyList<string> Errors);

/// <summary>Збережене вимірювання: сире й очищене значення.</summary>
public sealed record ReadingDto(DateTimeOffset Timestamp, double? RawValue, double? CleanValue, string Status);

/// <summary>Зафіксована аномалія.</summary>
public sealed record AnomalyDto(long Id, string SensorCode, DateTimeOffset Timestamp, string Kind, double? Value, string Details);

/// <summary>Звіт про якість даних датчика.</summary>
public sealed record QualityReportDto(
    string SensorCode,
    double Score,
    string Grade,
    double Completeness,
    double Validity,
    double AnomalyRate,
    int TotalReadings,
    int AnomalyCount);
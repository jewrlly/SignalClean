using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using SignalClean.Core;
using SignalClean.Data;

namespace SignalClean.Api.Services;

/// <summary>Результат створення датчика.</summary>
public sealed record CreateSensorResult(SensorDto? Sensor, Dictionary<string, string[]>? ValidationErrors, bool Conflict);

/// <summary>Реєстрація нових датчиків з перевіркою вхідних даних.</summary>
public sealed partial class SensorService(SignalCleanDbContext db, TimeProvider clock, QueryService queries)
{
    public async Task<CreateSensorResult> CreateAsync(CreateSensorRequest? request, CancellationToken ct)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return new CreateSensorResult(null, errors, false);
        }

        var code = IngestService.NormalizeCode(request!.Code);
        if (await db.Sensors.AnyAsync(s => s.Code == code, ct))
        {
            return new CreateSensorResult(null, null, true);
        }

        // Типів датчиків мало, тому порівнюємо назву без урахування регістру в пам'яті.
        var types = await db.SensorTypes.ToListAsync(ct);
        var type = types.FirstOrDefault(t => string.Equals(t.Name, request.TypeName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (type is null)
        {
            var known = string.Join(", ", types.Select(t => t.Name));
            return new CreateSensorResult(
                null,
                new Dictionary<string, string[]> { ["typeName"] = [$"Невідомий тип датчика. Доступні: {known}"] },
                false);
        }

        db.Sensors.Add(new Sensor
        {
            Code = code,
            Name = request.Name.Trim(),
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            SensorTypeId = type.Id,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(ct);

        return new CreateSensorResult(await queries.GetSensorAsync(code, ct), null, false);
    }

    internal static Dictionary<string, string[]> Validate(CreateSensorRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["body"] = ["Тіло запиту порожнє"];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Code) || !CodeRegex().IsMatch(request.Code.Trim()))
        {
            errors["code"] = ["Код: 2–32 символи, латинські літери, цифри або дефіс"];
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
        {
            errors["name"] = ["Назва обов'язкова, до 100 символів"];
        }

        if (string.IsNullOrWhiteSpace(request.TypeName))
        {
            errors["typeName"] = ["Тип датчика обов'язковий"];
        }

        if (request.Location is { Length: > 100 })
        {
            errors["location"] = ["Розташування до 100 символів"];
        }

        return errors;
    }

    [GeneratedRegex("^[A-Za-z0-9-]{2,32}$")]
    private static partial Regex CodeRegex();
}
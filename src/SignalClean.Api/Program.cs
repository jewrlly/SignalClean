using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;

using SignalClean.Api;
using SignalClean.Api.Services;
using SignalClean.Core;
using SignalClean.Data;

var builder = WebApplication.CreateBuilder(args);

// Рядок підключення читаємо в момент створення DbContext, щоб тести могли його підмінити.
builder.Services.AddDbContext<SignalCleanDbContext>((sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
        ?? "Data Source=signalclean.db";
    options.UseSqlite(connectionString);
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IngestService>();
builder.Services.AddScoped<QueryService>();
builder.Services.AddScoped<SensorService>();
builder.Services.AddProblemDetails();

// Датчики можуть надсилати NaN / Infinity: дозволяємо їх у JSON, щоб зафіксувати як аномалію, а не впасти.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals);

var app = builder.Build();

// Будь-який необроблений виняток → відповідь 500 у форматі ProblemDetails, сервер не падає.
app.UseExceptionHandler();
app.UseStatusCodePages();

using (var scope = app.Services.CreateScope())
{
    DbSeeder.Seed(scope.ServiceProvider.GetRequiredService<SignalCleanDbContext>());
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api");

api.MapGet("/sensors", async (QueryService q, CancellationToken ct) =>
    Results.Ok(await q.GetSensorsAsync(ct)));

api.MapGet("/sensors/{code}", async (string code, QueryService q, CancellationToken ct) =>
    await q.GetSensorAsync(code, ct) is { } sensor ? Results.Ok(sensor) : Results.NotFound());

api.MapPost("/sensors", async (CreateSensorRequest? request, SensorService sensors, CancellationToken ct) =>
{
    var result = await sensors.CreateAsync(request, ct);
    if (result.ValidationErrors is not null)
    {
        return Results.ValidationProblem(result.ValidationErrors);
    }

    if (result.Conflict)
    {
        return Results.Conflict(new { error = "Датчик з таким кодом уже існує" });
    }

    return Results.Created($"/api/sensors/{result.Sensor!.Code}", result.Sensor);
}).AddEndpointFilter<ApiKeyFilter>();

api.MapGet("/sensors/{code}/readings", async (string code, int? limit, QueryService q, CancellationToken ct) =>
    await q.GetReadingsAsync(code, limit ?? 100, ct) is { } list ? Results.Ok(list) : Results.NotFound());

api.MapGet("/sensors/{code}/anomalies", async (string code, int? limit, QueryService q, CancellationToken ct) =>
    await q.GetAnomaliesAsync(code, limit ?? 50, ct) is { } list ? Results.Ok(list) : Results.NotFound());

api.MapGet("/sensors/{code}/quality", async (string code, int? window, QueryService q, CancellationToken ct) =>
    await q.GetQualityAsync(code, window ?? QueryService.DefaultQualityWindow, ct) is { } report ? Results.Ok(report) : Results.NotFound());

api.MapPost("/readings", async (IngestRequest? request, IngestService ingest, CancellationToken ct) =>
{
    if (request?.Readings is null || request.Readings.Count == 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["readings"] = ["Пакет порожній"] });
    }

    if (request.Readings.Count > IngestService.MaxBatchSize)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["readings"] = [$"Забагато вимірювань у пакеті (максимум {IngestService.MaxBatchSize})"],
        });
    }

    return Results.Ok(await ingest.IngestAsync(request.Readings, ct));
}).AddEndpointFilter<ApiKeyFilter>();

app.Run();

/// <summary>Потрібен для інтеграційних тестів (WebApplicationFactory).</summary>
public partial class Program;
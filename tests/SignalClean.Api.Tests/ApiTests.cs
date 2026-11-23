using System.Net;
using System.Net.Http.Json;
using SignalClean.Api.Services;
using SignalClean.Core;

namespace SignalClean.Api.Tests;

// Кожен тест піднімає власний екземпляр API з чистою БД, тому тести незалежні.
public class ApiTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UtcNow.AddMinutes(-10);

    private static IngestRequest Batch(string code, params double?[] values) =>
        new(values.Select((v, i) => new ReadingInput(code, Start.AddSeconds(i), v)).ToList());

    private static async Task<IngestResult> PostReadingsAsync(HttpClient client, IngestRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/readings", request, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IngestResult>(ApiFactory.Json))!;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Sensors_AreSeeded()
    {
        using var factory = new ApiFactory();
        var sensors = await factory.CreateClient().GetFromJsonAsync<List<SensorDto>>("/api/sensors", ApiFactory.Json);

        Assert.NotNull(sensors);
        Assert.Equal(4, sensors.Count);
        Assert.Contains(sensors, s => s.Code == "TEMP-01" && s.Unit == "°C");
        Assert.All(sensors, s => Assert.Null(s.QualityScore)); // даних ще немає
    }

    [Fact]
    public async Task Ingest_WithoutApiKey_Returns401()
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateClient().PostAsJsonAsync("/api/readings", Batch("TEMP-01", 20), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task CreateSensor_WithoutApiKey_Returns401()
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/sensors",
            new CreateSensorRequest("TEMP-09", "Тест", "Temperature", null),
            ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithWrongApiKey_Returns401()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyFilter.HeaderName, "wrong");

        var response = await client.PostAsJsonAsync("/api/readings", Batch("TEMP-01", 20), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_EmptyBatch_Returns400()
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateAuthorizedClient()
            .PostAsJsonAsync("/api/readings", new IngestRequest([]), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_MalformedJson_Returns400()
    {
        using var factory = new ApiFactory();
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await factory.CreateAuthorizedClient().PostAsync("/api/readings", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_TooLargeBatch_Returns400()
    {
        using var factory = new ApiFactory();
        var values = Enumerable.Repeat<double?>(20, IngestService.MaxBatchSize + 1).ToArray();

        var response = await factory.CreateAuthorizedClient()
            .PostAsJsonAsync("/api/readings", Batch("TEMP-01", values), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_CleanData_AllAccepted_ScoreA()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateAuthorizedClient();

        var result = await PostReadingsAsync(client, Batch("TEMP-01", 20, 20.1, 19.9, 20.0, 20.2, 20.1));

        Assert.Equal(6, result.Accepted);
        Assert.Equal(0, result.Rejected);
        Assert.Equal(0, result.Anomalies);

        var quality = await client.GetFromJsonAsync<QualityReportDto>("/api/sensors/TEMP-01/quality", ApiFactory.Json);
        Assert.NotNull(quality);
        Assert.Equal(100, quality.Score);
        Assert.Equal("A", quality.Grade);
    }

    [Fact]
    public async Task Ingest_DirtyData_DetectsAnomalies_AndCleans()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateAuthorizedClient();

        // норма, норма, норма, норма, сплеск, пропуск, NaN, код помилки
        var result = await PostReadingsAsync(client, Batch("TEMP-01", 20, 20.1, 19.9, 20, 70, null, double.NaN, -999));

        Assert.Equal(6, result.Accepted);  // 5 коректних/виправлених + 1 пропуск
        Assert.Equal(2, result.Rejected);  // NaN та -999
        Assert.Equal(3, result.Anomalies); // сплеск, NaN, поза діапазоном

        var readings = await client.GetFromJsonAsync<List<ReadingDto>>("/api/sensors/temp-01/readings?limit=100", ApiFactory.Json);
        Assert.NotNull(readings);
        Assert.Equal(8, readings.Count);
        var spike = readings[4];
        Assert.Equal("Corrected", spike.Status);
        Assert.Equal(70, spike.RawValue);
        Assert.InRange(spike.CleanValue!.Value, 19.9, 20.1);

        var anomalies = await client.GetFromJsonAsync<List<AnomalyDto>>("/api/sensors/TEMP-01/anomalies", ApiFactory.Json);
        Assert.NotNull(anomalies);
        Assert.Contains(anomalies, a => a.Kind == "Spike");
        Assert.Contains(anomalies, a => a.Kind == "NotANumber");
        Assert.Contains(anomalies, a => a.Kind == "OutOfRange");

        var quality = await client.GetFromJsonAsync<QualityReportDto>("/api/sensors/TEMP-01/quality", ApiFactory.Json);
        Assert.NotNull(quality);
        Assert.True(quality.Score < 100);
    }

    [Fact]
    public async Task Ingest_UnknownSensor_IsRejected_OthersAccepted()
    {
        using var factory = new ApiFactory();
        var request = new IngestRequest(
        [
            new ReadingInput("NOPE-99", Start, 1),
            new ReadingInput("HUM-01", Start, 40),
            new ReadingInput("", Start, 1),
        ]);

        var result = await PostReadingsAsync(factory.CreateAuthorizedClient(), request);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(2, result.Rejected);
        Assert.Contains(result.Errors, e => e.Contains("NOPE-99", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnknownSensor_Endpoints_Return404()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/sensors/NOPE")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/sensors/NOPE/readings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/sensors/NOPE/anomalies")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/sensors/NOPE/quality")).StatusCode);
    }

    [Fact]
    public async Task Readings_LimitIsRespected()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateAuthorizedClient();
        await PostReadingsAsync(client, Batch("PRES-01", 1000, 1001, 1002, 1003, 1004));

        var readings = await client.GetFromJsonAsync<List<ReadingDto>>("/api/sensors/PRES-01/readings?limit=2", ApiFactory.Json);

        Assert.NotNull(readings);
        Assert.Equal(2, readings.Count);
        Assert.Equal(1004, readings[^1].RawValue); // останні, у хронологічному порядку
    }

    [Fact]
    public async Task CreateSensor_Valid_Returns201()
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateAuthorizedClient().PostAsJsonAsync(
            "/api/sensors",
            new CreateSensorRequest("temp-02", "Серверна", "temperature", "Корпус 1"),
            ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sensor = await response.Content.ReadFromJsonAsync<SensorDto>(ApiFactory.Json);
        Assert.Equal("TEMP-02", sensor!.Code);
        Assert.Equal("Temperature", sensor.Type);
    }

    [Fact]
    public async Task CreateSensor_Duplicate_Returns409()
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateAuthorizedClient().PostAsJsonAsync(
            "/api/sensors",
            new CreateSensorRequest("TEMP-01", "Дубль", "Temperature", null),
            ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("x", "Назва", "Temperature")]          // закороткий код
    [InlineData("BAD CODE", "Назва", "Temperature")]   // пробіл у коді
    [InlineData("OK-1", "", "Temperature")]            // порожня назва
    [InlineData("OK-1", "Назва", "Radiation")]         // невідомий тип
    public async Task CreateSensor_Invalid_Returns400(string code, string name, string type)
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateAuthorizedClient().PostAsJsonAsync(
            "/api/sensors",
            new CreateSensorRequest(code, name, type, null),
            ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
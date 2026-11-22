using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SignalClean.Core;

namespace SignalClean.Client.Services;

/// <summary>HTTP-клієнт до SignalClean API.</summary>
public sealed class ApiClient : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly HttpClient _http;
    private Uri _baseUri = new("http://localhost:5080/");

    public ApiClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>Перевіряє та встановлює адресу API. Повертає false, якщо адреса некоректна.</summary>
    public bool TrySetBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        string normalized = url.Trim();
        if (!normalized.EndsWith('/'))
        {
            normalized += "/";
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        _baseUri = uri;
        return true;
    }

    public Task<List<SensorDto>?> GetSensorsAsync(CancellationToken ct = default) =>
        GetAsync<List<SensorDto>>("api/sensors", ct);

    public Task<List<ReadingDto>?> GetReadingsAsync(string code, int limit, CancellationToken ct = default) =>
        GetAsync<List<ReadingDto>>($"api/sensors/{Uri.EscapeDataString(code)}/readings?limit={limit}", ct);

    public Task<List<AnomalyDto>?> GetAnomaliesAsync(string code, int limit, CancellationToken ct = default) =>
        GetAsync<List<AnomalyDto>>($"api/sensors/{Uri.EscapeDataString(code)}/anomalies?limit={limit}", ct);

    public Task<QualityReportDto?> GetQualityAsync(string code, CancellationToken ct = default) =>
        GetAsync<QualityReportDto>($"api/sensors/{Uri.EscapeDataString(code)}/quality", ct);

    public void Dispose() => _http.Dispose();

    private Task<T?> GetAsync<T>(string relative, CancellationToken ct) =>
        _http.GetFromJsonAsync<T>(new Uri(_baseUri, relative), Json, ct);
}

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SignalClean.Api.Tests;

/// <summary>Запускає API в пам'яті з окремою тимчасовою SQLite-базою для кожного тестового класу.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-key";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"signalclean-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath};Pooling=False");
        builder.UseSetting("Ingest:ApiKey", ApiKey);
    }

    public HttpClient CreateAuthorizedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyFilter.HeaderName, ApiKey);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // Файл ще може бути зайнятий: це тимчасова папка, тож не критично.
        }
    }
}
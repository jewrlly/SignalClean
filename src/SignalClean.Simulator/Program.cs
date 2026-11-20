using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SignalClean.Core;
using SignalClean.Simulator;

// Симулятор мережі датчиків: щосекунди надсилає в API пакет вимірювань із випадковими дефектами.
// Аргументи: --api http://localhost:5080  --key dev-key-change-me  --interval 1000  --clean  --seed 42

var options = SimulatorOptions.Parse(args);
if (options is null)
{
    Console.WriteLine("Використання: SignalClean.Simulator [--api URL] [--key КЛЮЧ] [--interval МС] [--seed N] [--clean]");
    return 1;
}

var random = options.Seed is int seed ? new Random(seed) : new Random();
var defects = options.Clean ? DefectProfile.None : new DefectProfile();
var sensors = new[]
{
    new SensorModel("TEMP-01", baseline: 22, amplitude: 3, noise: 0.15, periodSeconds: 300, defects, random),
    new SensorModel("HUM-01", baseline: 45, amplitude: 8, noise: 0.5, periodSeconds: 420, defects, random),
    new SensorModel("PRES-01", baseline: 1013, amplitude: 4, noise: 0.3, periodSeconds: 600, defects, random),
    new SensorModel("CO2-01", baseline: 650, amplitude: 150, noise: 10, periodSeconds: 240, defects, random),
};

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
};

using var http = new HttpClient { BaseAddress = options.ApiUrl, Timeout = TimeSpan.FromSeconds(5) };
http.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Симулятор запущено → {options.ApiUrl} (інтервал {options.Interval.TotalMilliseconds} мс). Ctrl+C — зупинити.");

int sent = 0;
using var timer = new PeriodicTimer(options.Interval);
try
{
    do
    {
        var now = DateTimeOffset.UtcNow;
        var batch = new List<ReadingInput>();
        foreach (var sensor in sensors)
        {
            if (sensor.TryNext(now, out var value))
            {
                batch.Add(new ReadingInput(sensor.Code, now, value));
            }
        }

        if (batch.Count == 0)
        {
            continue;
        }

        try
        {
            using var response = await http.PostAsJsonAsync("api/readings", new IngestRequest(batch), json, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<IngestResult>(json, cts.Token);
                sent += batch.Count;
                Console.WriteLine($"{now:HH:mm:ss} надіслано {batch.Count}, прийнято {result?.Accepted}, відхилено {result?.Rejected}, аномалій {result?.Anomalies} (всього {sent})");
            }
            else
            {
                Console.WriteLine($"{now:HH:mm:ss} API відповів {(int)response.StatusCode} {response.ReasonPhrase}");
            }
        }
        catch (HttpRequestException ex)
        {
            // API ще не запущено або мережа недоступна: не падаємо, пробуємо на наступному такті.
            Console.WriteLine($"{now:HH:mm:ss} API недоступний: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cts.IsCancellationRequested)
        {
            Console.WriteLine($"{now:HH:mm:ss} тайм-аут запиту");
        }
    }
    while (await timer.WaitForNextTickAsync(cts.Token));
}
catch (OperationCanceledException)
{
    // Ctrl+C
}

Console.WriteLine($"Зупинено. Надіслано вимірювань: {sent}");
return 0;

internal sealed record SimulatorOptions(Uri ApiUrl, string ApiKey, TimeSpan Interval, int? Seed, bool Clean)
{
    public static SimulatorOptions? Parse(string[] args)
    {
        var api = new Uri("http://localhost:5080/");
        string key = "dev-key-change-me";
        int intervalMs = 1000;
        int? seed = null;
        bool clean = false;

        for (int i = 0; i < args.Length; i++)
        {
            string? next = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--api" when next is not null && Uri.TryCreate(next.EndsWith('/') ? next : next + "/", UriKind.Absolute, out var uri):
                    api = uri;
                    i++;
                    break;
                case "--key" when !string.IsNullOrWhiteSpace(next):
                    key = next;
                    i++;
                    break;
                case "--interval" when int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ms) && ms >= 100:
                    intervalMs = ms;
                    i++;
                    break;
                case "--seed" when int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out int s):
                    seed = s;
                    i++;
                    break;
                case "--clean":
                    clean = true;
                    break;
                default:
                    return null;
            }
        }

        return new SimulatorOptions(api, key, TimeSpan.FromMilliseconds(intervalMs), seed, clean);
    }
}

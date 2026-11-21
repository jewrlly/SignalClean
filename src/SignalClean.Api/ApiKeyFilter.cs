using System.Security.Cryptography;
using System.Text;

namespace SignalClean.Api;

/// <summary>
/// Захищає ендпоінти запису: запит має містити заголовок X-Api-Key з ключем із конфігурації (Ingest:ApiKey).
/// </summary>
public sealed class ApiKeyFilter(IConfiguration configuration) : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        string? expected = configuration["Ingest:ApiKey"];
        if (string.IsNullOrEmpty(expected))
        {
            return Results.Problem("API-ключ не налаштовано на сервері (Ingest:ApiKey)", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
            || !FixedTimeEquals(provided.ToString(), expected))
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }

    // Порівняння за сталий час: не дає підбирати ключ за часом відповіді.
    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
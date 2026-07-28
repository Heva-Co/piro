using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Piro.Cli;

/// <summary>
/// Talks to a Piro instance's config endpoints. Authenticates with <c>X-Api-Key</c>, which the server
/// accepts on any endpoint and resolves to the same claims a JWT would, so no backend work is needed
/// for this phase (RFC 0019 §4.6).
/// </summary>
internal sealed class PiroApiClient : IDisposable
{
    private readonly HttpClient _http;

    public PiroApiClient(Settings settings)
    {
        _http = new HttpClient { BaseAddress = new Uri(settings.Url + "/"), Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("piro-cli", Version));
    }

    public static string Version =>
        typeof(PiroApiClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public Task<ConfigPlanDto> PlanAsync(ConfigApplyRequest request, CancellationToken ct) =>
        PostPlanAsync("api/v1/config/plan", request, ct);

    public Task<ConfigPlanDto> ApplyAsync(ConfigApplyRequest request, CancellationToken ct) =>
        PostPlanAsync("api/v1/config/apply", request, ct);

    private async Task<ConfigPlanDto> PostPlanAsync(string route, ConfigApplyRequest request, CancellationToken ct)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, route)
            {
                Content = JsonContent.Create(request, CliJsonContext.Default.ConfigApplyRequest),
            }, ct);

        // A validation failure comes back as 400 carrying the plan itself, so the body is the answer
        // rather than an error to translate — every located error reaches the user in one pass. Any
        // other 400 (or a proxy's HTML error page on the way) is not a plan, so fall through to the
        // status-based message rather than reporting a JSON parse error the user cannot act on.
        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest
            && response.Content.Headers.ContentType?.MediaType is "application/json"
                or "application/problem+json")
        {
            var plan = await ReadJsonAsync(response, CliJsonContext.Default.ConfigPlanDto, ct);
            if (plan is not null) return plan;
        }

        throw await FailureAsync(response, ct);
    }

    public async Task<string> ExportAsync(CancellationToken ct)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "api/v1/config/export"), ct);

        if (!response.IsSuccessStatusCode) throw await FailureAsync(response, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// The authenticated identity, printed before acting so a developer with several instances
    /// configured cannot apply staging config to production without having seen the target (§4.6).
    /// Best-effort: an instance that does not expose it must not block the command.
    /// </summary>
    public async Task<CurrentUserDto?> WhoAmIAsync(CancellationToken ct)
    {
        try
        {
            using var response = await SendAsync(
                () => new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/me"), ct);
            return response.IsSuccessStatusCode
                ? await ReadJsonAsync(response, CliJsonContext.Default.CurrentUserDto, ct)
                : null;
        }
        catch (CliException)
        {
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> request, CancellationToken ct)
    {
        try
        {
            return await _http.SendAsync(request(), ct);
        }
        catch (HttpRequestException ex)
        {
            throw new CliException($"Could not reach {_http.BaseAddress}: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CliException($"Timed out talking to {_http.BaseAddress}.");
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(
        HttpResponseMessage response, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync(typeInfo, ct);
        }
        catch (JsonException ex)
        {
            throw new CliException($"The server returned a response the CLI could not read: {ex.Message}");
        }
    }

    private static async Task<CliException> FailureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);

        return new CliException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "Authentication failed. Check that PIRO_API_KEY is a valid Full-scope key.",
            HttpStatusCode.Forbidden =>
                "This API key lacks permission. Config as code requires an Owner or Admin key.",
            HttpStatusCode.NotFound =>
                "This Piro instance has no config endpoints — it predates config as code.",
            _ => $"The server returned {(int)response.StatusCode} {response.ReasonPhrase}."
                 + (string.IsNullOrWhiteSpace(body) ? "" : $"\n{Truncate(body)}"),
        });
    }

    private static string Truncate(string body) =>
        body.Length <= 2000 ? body : body[..2000] + "…";

    public void Dispose() => _http.Dispose();
}

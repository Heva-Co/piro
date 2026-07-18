using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>
/// PagerDuty REST API v2 discovery: lists services and their Events API v2 integration keys, and can
/// provision a fresh Events API v2 integration when a service has none. Authenticated with the OAuth
/// bearer token from <see cref="IOAuthTokenProvider"/> (RFC 0004 §4.4).
/// </summary>
internal class PagerDutyDiscoveryService(
    IOAuthTokenProvider tokenProvider,
    IHttpClientFactory httpClientFactory) : IPagerDutyDiscoveryService
{
    private const string ApiBase = "https://api.pagerduty.com";
    private const string EventsApiV2Type = "events_api_v2_inbound_integration";
    private const string HttpClientName = "oauth-integration-http";

    public async Task<IReadOnlyList<DiscoveredPagerDutyService>> ListServicesAsync(Guid integrationId, CancellationToken ct = default)
    {
        var http = await CreateAuthedClientAsync(integrationId, ct);

        var result = new List<DiscoveredPagerDutyService>();
        var offset = 0;
        const int limit = 100;
        bool more;
        do
        {
            var url = $"/services?include[]=integrations&limit={limit}&offset={offset}";
            var json = await http.GetFromJsonAsync<JsonElement>(url, ct);

            if (json.TryGetProperty("services", out var services))
            {
                foreach (var svc in services.EnumerateArray())
                {
                    var id = svc.GetProperty("id").GetString()!;
                    var name = svc.TryGetProperty("name", out var n) ? n.GetString() ?? id : id;
                    result.Add(new DiscoveredPagerDutyService(id, name, ExtractEventsV2Key(svc)));
                }
            }

            more = json.TryGetProperty("more", out var m) && m.GetBoolean();
            offset += limit;
        } while (more);

        return result;
    }

    public async Task<string> ResolveRoutingKeyAsync(Guid integrationId, string pagerDutyServiceId, CancellationToken ct = default)
    {
        var http = await CreateAuthedClientAsync(integrationId, ct);

        // Read the service with its integrations; reuse an existing Events API v2 key if present.
        var svcJson = await http.GetFromJsonAsync<JsonElement>(
            $"/services/{pagerDutyServiceId}?include[]=integrations", ct);
        if (svcJson.TryGetProperty("service", out var svc))
        {
            var existing = ExtractEventsV2Key(svc);
            if (!string.IsNullOrEmpty(existing))
                return existing;
        }

        // None exists — provision a fresh "Piro" Events API v2 integration (requires services.write).
        var body = new
        {
            integration = new
            {
                type = EventsApiV2Type,
                name = "Piro"
            }
        };
        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await http.PostAsync($"/services/{pagerDutyServiceId}/integrations", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(response.StatusCode == System.Net.HttpStatusCode.Forbidden
                ? "PagerDuty denied provisioning a routing key (403). The connection likely lacks the 'services.write' scope — disconnect and reconnect the PagerDuty integration to grant it."
                : $"PagerDuty rejected provisioning a routing key ({(int)response.StatusCode}): {errorBody}");
        }

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var key = created.TryGetProperty("integration", out var integ)
            && integ.TryGetProperty("integration_key", out var k)
            ? k.GetString()
            : null;

        return key ?? throw new InvalidOperationException(
            $"PagerDuty did not return an integration_key when provisioning on service {pagerDutyServiceId}.");
    }

    /// <summary>Pulls the first Events API v2 integration_key out of a service object's integrations array, if any.</summary>
    private static string? ExtractEventsV2Key(JsonElement service)
    {
        if (!service.TryGetProperty("integrations", out var integrations) || integrations.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var integ in integrations.EnumerateArray())
        {
            var type = integ.TryGetProperty("type", out var t) ? t.GetString() : null;
            // The type on a returned integration is often "events_api_v2_inbound_integration_reference".
            if (type is not null && type.StartsWith(EventsApiV2Type, StringComparison.Ordinal)
                && integ.TryGetProperty("integration_key", out var k)
                && k.GetString() is { Length: > 0 } key)
                return key;
        }
        return null;
    }

    private async Task<HttpClient> CreateAuthedClientAsync(Guid integrationId, CancellationToken ct)
    {
        var token = await tokenProvider.GetAccessTokenAsync(integrationId, ct);
        var http = httpClientFactory.CreateClient(HttpClientName);
        http.BaseAddress = new Uri(ApiBase);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // PagerDuty REST API v2 requires this Accept header.
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.pagerduty+json", 1.0)
        {
            Parameters = { new NameValueHeaderValue("version", "2") }
        });
        return http;
    }
}

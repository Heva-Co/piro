using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Piro.Contracts;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Jira;

/// <summary>
/// Discovers the Jira Cloud <c>cloudId</c>/site URL for a connected integration and stores them on its
/// config (RFC 0012). Lives in the Jira assembly and reaches Piro only through
/// <see cref="IIntegrationHost"/> and <see cref="IOAuthTokenProvider"/> — it never touches a repository
/// or the DbContext. It obtains the bearer token from the token provider and writes only the non-secret
/// coordinates (cloudId, siteUrl) back through the host's bounded config-write seam.
/// </summary>
public sealed class JiraDiscoveryService(
    IOAuthTokenProvider tokenProvider,
    IIntegrationHost host,
    IHttpClientFactory httpClientFactory) : IJiraDiscoveryService
{
    private const string AccessibleResourcesUrl = "https://api.atlassian.com/oauth/token/accessible-resources";
    private const string HttpClientName = "oauth-integration-http";

    public async Task DiscoverAndStoreCloudAsync(Guid integrationId, CancellationToken ct = default)
    {
        var token = await tokenProvider.GetAccessTokenAsync(integrationId, ct);
        var http = httpClientFactory.CreateClient(HttpClientName);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // [{ id: <cloudId>, url: <siteUrl>, name, scopes[], avatarUrl }, ...] — one entry per accessible site.
        var resources = await http.GetFromJsonAsync<List<JsonElement>>(AccessibleResourcesUrl, ct);
        var first = resources is { Count: > 0 } ? resources[0] : (JsonElement?)null;
        if (first is null)
            throw new InvalidOperationException(
                "Jira returned no accessible resources for this connection — the OAuth app may lack Jira access.");

        var cloudId = first.Value.TryGetProperty("id", out var id) ? id.GetString() : null;
        var siteUrl = first.Value.TryGetProperty("url", out var url) ? url.GetString() : null;
        if (string.IsNullOrWhiteSpace(cloudId))
            throw new InvalidOperationException("Jira accessible-resources response had no cloudId.");

        // Persist the non-secret coordinates through the host — never touches the ConfigJson directly.
        await host.SetConfigValuesAsync(integrationId, new Dictionary<string, string?>
        {
            ["cloudId"] = cloudId,
            ["siteUrl"] = siteUrl,
        }, ct);
    }
}

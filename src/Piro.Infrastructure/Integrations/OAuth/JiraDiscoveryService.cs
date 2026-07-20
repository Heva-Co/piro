using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Piro.Application.Interfaces;

namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>
/// Discovers the Jira Cloud <c>cloudId</c>/site URL for a connected integration and stores them on its
/// config (RFC 0012). Authenticated with the OAuth bearer token from <see cref="IOAuthTokenProvider"/>.
/// Only non-secret coordinates (cloudId, siteUrl) are written, merged into the stored ConfigJson
/// without touching the already-encrypted <c>clientSecret</c>.
/// </summary>
internal sealed class JiraDiscoveryService(
    IOAuthTokenProvider tokenProvider,
    IIntegrationRepository integrationRepo,
    IHttpClientFactory httpClientFactory) : IJiraDiscoveryService
{
    private const string AccessibleResourcesUrl = "https://api.atlassian.com/oauth/token/accessible-resources";
    private const string HttpClientName = "oauth-integration-http";

    public async Task DiscoverAndStoreCloudAsync(Guid integrationId, CancellationToken ct = default)
    {
        var integration = await integrationRepo.GetByIdAsync(integrationId, ct)
            ?? throw new InvalidOperationException($"Integration {integrationId} not found.");

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

        // Merge cloudId/siteUrl into the stored ConfigJson without disturbing the encrypted clientSecret.
        var config = JsonNode.Parse(integration.ConfigJson) as JsonObject ?? new JsonObject();
        config["cloudId"] = cloudId;
        config["siteUrl"] = siteUrl;
        integration.ConfigJson = config.ToJsonString();

        await integrationRepo.UpdateAsync(integration, ct);
    }
}

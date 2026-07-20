namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>
/// Discovers and persists the Jira Cloud <c>cloudId</c> and human-facing site URL for an OAuth-connected
/// Jira integration (RFC 0012), using the stored token. 3LO API calls route through
/// <c>https://api.atlassian.com/ex/jira/{cloudId}</c>, so the <c>cloudId</c> must be captured once at
/// connect time and stored on the integration. Provider-specific — reuses the generic OAuth token
/// provider unchanged, like <c>IPagerDutyDiscoveryService</c>.
/// </summary>
public interface IJiraDiscoveryService
{
    /// <summary>
    /// Reads the account's first accessible Jira resource (cloudId + site URL) via
    /// <c>GET /oauth/token/accessible-resources</c> and stores it on the integration's config.
    /// Called after the OAuth callback succeeds for a Jira integration.
    /// </summary>
    Task DiscoverAndStoreCloudAsync(Guid integrationId, CancellationToken ct = default);
}

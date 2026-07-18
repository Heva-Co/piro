namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>
/// Discovers PagerDuty services (and their Events API v2 routing keys) for an OAuth-connected
/// integration, using the stored token (RFC 0004 §4.4). Provider-specific — a GitHub/Jira consumer
/// would have its own discovery, reusing the generic OAuth token provider unchanged.
/// </summary>
public interface IPagerDutyDiscoveryService
{
    /// <summary>Lists the account's PagerDuty services with any existing Events API v2 routing key.</summary>
    Task<IReadOnlyList<DiscoveredPagerDutyService>> ListServicesAsync(Guid integrationId, CancellationToken ct = default);

    /// <summary>
    /// Returns a usable Events API v2 routing key for a PagerDuty service — reading an existing one,
    /// or provisioning a fresh "Piro" integration on that service if none exists (requires services.write).
    /// </summary>
    Task<string> ResolveRoutingKeyAsync(Guid integrationId, string pagerDutyServiceId, CancellationToken ct = default);
}

/// <summary>A PagerDuty service as seen during discovery.</summary>
public sealed record DiscoveredPagerDutyService(
    string Id,
    string Name,
    string? RoutingKey);

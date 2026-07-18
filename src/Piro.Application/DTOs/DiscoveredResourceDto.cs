namespace Piro.Application.DTOs;

/// <summary>A remote resource discovered for an OAuth-connected integration (RFC 0004 §4.4a). For
/// PagerDuty, one PagerDuty service; RoutingKey is null when the service has no Events API v2 key yet
/// (Piro provisions one when the mapping is confirmed).</summary>
public record DiscoveredResourceDto(
    string RemoteId,
    string Label,
    string? RoutingKey);

using Piro.Domain.Enums;

namespace Piro.Application.Models.NotificationEvents;

/// <summary>
/// Payload for <c>system:integration:expired</c> (RFC 0009 §4.2) — an integration's connection
/// expired or was disconnected and it can no longer be used until reconnected. A platform-health
/// event about Piro itself, not about a monitored service. Additive-only.
/// </summary>
public record IntegrationExpiredPayload(
    Guid IntegrationId,
    string IntegrationName,
    IntegrationType Type,
    /// <summary>Human-readable reason, if known (e.g. "OAuth token expired", "webhook revoked").</summary>
    string? Reason,
    DateTimeOffset ExpiredAt
) : INotificationEvent
{
    public string EventType => "system:integration:expired";
    public int Version => 1;
}

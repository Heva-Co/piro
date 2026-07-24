using System.Text.Json.Serialization;

namespace Piro.Integrations.Gcp;

/// <summary>
/// GCP Cloud Monitoring's webhook notification payload (v1.2) — see RFC 0001 §4.8 and
/// https://docs.cloud.google.com/monitoring/support/notification-options#webhooks. Only the
/// fields Piro actually reads are modeled; the real payload has more.
/// </summary>
public record GcpCloudMonitoringWebhookPayload(
    [property: JsonPropertyName("incident")] GcpCloudMonitoringIncident? Incident
);

public record GcpCloudMonitoringIncident(
    [property: JsonPropertyName("incident_id")] string? IncidentId,
    /// <summary>"open" or "closed" — see <see cref="GcpIncidentState"/>.</summary>
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("policy_name")] string? PolicyName,
    /// <summary>"critical" or "warning" — mapped to ServiceStatus, RFC 0001 §4.8.</summary>
    [property: JsonPropertyName("severity")] string? Severity,
    /// <summary>Google Cloud console URL for this incident — stored on Alert.SourceUrl, generic across sources.</summary>
    [property: JsonPropertyName("url")] string? Url
);

public static class GcpIncidentState
{
    public const string Open = "open";
    public const string Closed = "closed";
}

using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Gcp;

/// <summary>
/// GCP Cloud Monitoring, as a self-describing integration (RFC 0016). Lives in its own assembly,
/// referencing only the integration contract — it knows nothing of Piro's internals. This is an
/// <b>inbound</b> integration: it has no dispatcher. What the assembly contributes is its identity
/// plus a manifest declaring <see cref="IntegrationCapability.CreatesAlerts"/> and a
/// <see cref="IntegrationManifest.WebhookPath"/>; the actual ingestion (auth-token HMAC validation
/// and Alert-row creation) stays in Piro's Application/core layer and is not part of this assembly.
/// </summary>
public sealed class GcpCloudMonitoringWebhookIntegration : IIntegration
{
    public string IntegrationId => "GcpCloudMonitoringWebhook";

    public IntegrationManifest Manifest => new()
    {
        Category = IntegrationCategory.ThirdParty,
        Capabilities = IntegrationCapability.CreatesAlerts | IntegrationCapability.SupportsEscalationPolicy,
        ConfigType = typeof(GcpCloudMonitoringWebhookConfig),
        Label = "GCP Cloud Monitoring",
        Description = "Receive alerting policy notifications from Google Cloud Monitoring as Alerts.",
        IconifyIcon = "logos:google-cloud",
        WebhookPath = "gcp",
    };
}

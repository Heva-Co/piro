using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Gcp;

/// <summary>
/// GCP Cloud Monitoring, as a self-describing integration (RFC 0016). Lives in its own assembly,
/// referencing only the integration contract — it knows nothing of Piro's internals. This is an
/// <b>inbound</b> integration: it has no notification dispatcher. It contributes its identity, a
/// manifest declaring <see cref="IntegrationCapability.CreatesAlerts"/>, and an
/// <see cref="IInboundWebhookHandler"/> (registered in <see cref="Configure"/>) that does the whole
/// ingestion — auth-token validation, payload parsing, and pushing alerts through
/// <see cref="IAlertService"/>. Nothing GCP-specific remains in Piro's core.
/// </summary>
public sealed class GcpCloudMonitoringWebhookIntegration : IIntegration
{
    public string IntegrationId => "GcpCloudMonitoringWebhook";

    public IntegrationManifest Manifest => new()
    {
        Capabilities = IntegrationCapability.CreatesAlerts | IntegrationCapability.SupportsEscalationPolicy,
        ConfigType = typeof(GcpCloudMonitoringWebhookConfig),
        Label = "GCP Cloud Monitoring",
        Description = "Receive alerting policy notifications from Google Cloud Monitoring as Alerts.",
        IconifyIcon = "logos:google-cloud",
    };

    /// <summary>Registers the inbound webhook handler so Piro dispatches GCP posts to it.</summary>
    public void Configure(IIntegrationHost host)
    {
        var webhooks = host.GetRequiredService<IWebhookHost>();
        webhooks.RegisterWebhook(new GcpCloudMonitoringWebhookHandler());
    }
}

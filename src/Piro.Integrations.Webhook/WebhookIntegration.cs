using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Webhook;

/// <summary>
/// Generic outbound webhook, as a self-describing integration (RFC 0016). Lives in its own assembly,
/// referencing only the integration contract — it knows nothing of Piro's internals. Declares its
/// identity and manifest; its delivery behavior is <see cref="WebhookNotificationDispatcher"/>.
/// </summary>
public sealed class WebhookIntegration : IIntegration
{
    public string IntegrationId => "Webhook";

    public IntegrationManifest Manifest => new()
    {
        Category = IntegrationCategory.Notification,
        Capabilities = IntegrationCapability.SendsChannelNotification | IntegrationCapability.SubscribesToEvents,
        ConfigType = typeof(WebhookConfig),
        Label = "Webhook",
        Description = "POST a JSON payload to any URL when an incident or alert event fires (Zapier/Make compatible).",
        IconifyIcon = "tabler:webhook",
        SupportedEvents =
        [
            "alert:created",
            "alert:acknowledged",
            "alert:resolved",
            "incident:created",
            "incident:resolved",
        ],
    };
}

using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Ntfy;

/// <summary>
/// Ntfy, as a self-describing integration (RFC 0016). Lives in its own assembly, referencing only
/// the integration contract — it knows nothing of Piro's internals. Declares its identity and
/// manifest; its delivery behavior is <see cref="NtfyNotificationDispatcher"/>.
/// </summary>
public sealed class NtfyIntegration : IIntegration
{
    public string IntegrationId => "Ntfy";

    public IntegrationManifest Manifest => new()
    {
        Capabilities = IntegrationCapability.SendsPersonalNotification | IntegrationCapability.SubscribesToEvents,
        ConfigType = typeof(NtfyConfig),
        Label = "Ntfy",
        Description = "Publish alert notifications to a self-hosted or public ntfy topic.",
        IconifyIcon = "simple-icons:ntfy",
        SupportedEvents = ["alert:created", "alert:acknowledged", "alert:resolved"],
    };
}

using Piro.Integrations.Abstractions;

namespace Piro.Integrations.GoogleChat;

/// <summary>
/// Google Chat, as a self-describing integration (RFC 0016). Lives in its own assembly, referencing
/// only the integration contract — it knows nothing of Piro's internals. Declares its identity and
/// manifest; its delivery behavior is <see cref="GoogleChatNotificationDispatcher"/>.
/// </summary>
public sealed class GoogleChatIntegration : IIntegration
{
    public string IntegrationId => "GoogleChat";

    public IntegrationManifest Manifest => new()
    {
        Capabilities = IntegrationCapability.SendsChannelNotification | IntegrationCapability.SubscribesToEvents,
        ConfigType = typeof(GoogleChatConfig),
        Label = "Google Chat",
        Description = "Post alert notifications to a Google Chat space via an incoming webhook.",
        IconifyIcon = "logos:google-meet",
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

using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Telegram;

/// <summary>
/// Telegram, as a self-describing integration (RFC 0016). Lives in its own assembly, referencing only
/// the integration contract — it knows nothing of Piro's internals. Declares its identity and manifest;
/// its delivery behavior is <see cref="TelegramNotificationDispatcher"/>.
/// </summary>
public sealed class TelegramIntegration : IIntegration
{
    public string IntegrationId => "Telegram";

    public IntegrationManifest Manifest => new()
    {
        Capabilities = IntegrationCapability.SendsPersonalNotification | IntegrationCapability.SubscribesToEvents,
        ConfigType = typeof(TelegramConfig),
        Label = "Telegram",
        Description = "Send alert notifications to Telegram chats via a bot.",
        IconifyIcon = "logos:telegram",
        SupportedEvents = ["alert:*", "incident:*"],
    };
}

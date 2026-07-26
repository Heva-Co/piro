using Piro.Integrations.Abstractions;

namespace Piro.Integrations.MobilePush;

/// <summary>
/// The MobilePush integration descriptor (RFC 0016): delivers on-call pages to a user's registered
/// mobile devices (the Piro app) via FCM (Android) and APNs (iOS). It is a personal channel — one
/// "MobilePush" preference per user fans out to every device that user has registered — and it
/// subscribes to the alert/incident catalog events. Pure data; behavior lives in
/// <see cref="MobilePushNotificationDispatcher"/>.
/// </summary>
public sealed class MobilePushIntegration : IIntegration
{
    public string IntegrationId => "MobilePush";

    public IntegrationManifest Manifest => new()
    {
        Capabilities = IntegrationCapability.SendsPersonalNotification | IntegrationCapability.SubscribesToEvents,
        ConfigType = typeof(MobilePushConfig),
        Label = "Mobile push",
        Description = "Send on-call pages to the Piro mobile app on a user's phones (Android via FCM, iOS via APNs).",
        IconifyIcon = "mdi:cellphone-message",
        SupportedEvents =
        [
            "alert:created", "alert:acknowledged", "alert:resolved",
            "incident:created", "incident:resolved",
        ],
    };
}

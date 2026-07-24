using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Twilio;

/// <summary>
/// Twilio SMS, as a self-describing integration (RFC 0016). Its own assembly, carrying the Twilio SDK
/// so no other assembly compiles against it. Declares identity + manifest; delivery is
/// <see cref="TwilioNotificationDispatcher"/>.
/// </summary>
public sealed class TwilioIntegration : IIntegration
{
    public string IntegrationId => "Twilio";

    public IntegrationManifest Manifest => new()
    {
        Capabilities = IntegrationCapability.SendsPersonalNotification | IntegrationCapability.SubscribesToEvents,
        ConfigType = typeof(TwilioConfig),
        Label = "Twilio",
        Description = "Send SMS alerts through Twilio.",
        IconifyIcon = "logos:twilio-icon",
        SupportedEvents = ["alert:created", "alert:acknowledged", "alert:resolved"],
    };
}

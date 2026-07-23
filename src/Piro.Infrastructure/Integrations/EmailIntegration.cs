using Piro.Domain.Integrations.Config;
using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Integrations;

/// <summary>
/// Email as a self-describing integration (RFC 0016). Unlike the other integrations, Email is NOT in
/// its own assembly: its SMTP transport is core infrastructure shared with account-setup and
/// password-reset (RFC 0014), so its <see cref="IIntegration"/> lives here in Piro.Infrastructure
/// alongside that transport (RFC 0016 §4.1). Identity + manifest only; delivery + verification stay
/// on the existing EmailDispatcher.
/// </summary>
internal sealed class EmailIntegration : IIntegration
{
    public string IntegrationId => "Email";

    public IntegrationManifest Manifest => new()
    {
        Category = IntegrationCategory.Notification,
        Capabilities = IntegrationCapability.SendsPersonalNotification | IntegrationCapability.SubscribesToEvents,
        ConfigType = typeof(EmptyConfig),
        ChannelOnly = true,
        Creatable = false,
        Label = "Email",
        Description = "Send alert emails via the platform's configured SMTP/Resend setup.",
        IconifyIcon = "logos:google-gmail",
        SupportedEvents = ["alert:created", "alert:acknowledged", "alert:resolved", "incident:created", "incident:resolved"],
    };
}

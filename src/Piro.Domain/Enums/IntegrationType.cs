using Piro.Domain.Attributes;
using Piro.Domain.Integrations.Config;

namespace Piro.Domain.Enums;

public enum IntegrationType
{
    // Third-party integrations
    [IntegrationManifest(
        IntegrationCategory.ThirdParty,
        IntegrationDirection.Outbound,
        IntegrationCapability.RequiredByCheckType,
        typeof(GoogleCloudConfig),
        Label = "Google Cloud",
        Description = "Run Cloud Run Job checks against your GCP project.",
        IconifyIcon = "logos:google-cloud"
    )]
    GoogleCloud = 0,

    [IntegrationManifest(
        IntegrationCategory.ThirdParty,
        IntegrationDirection.Outbound,
        IntegrationCapability.None,
        typeof(JiraConfig),
        Label = "Jira",
        Description = "Create and track Jira tickets from alerts.",
        IconifyIcon = "logos:jira"
    )]
    Jira = 1,

    // Notification integrations (formerly NotificationChannelType)
    [IntegrationManifest(
        IntegrationCategory.Notification,
        IntegrationDirection.Outbound,
        IntegrationCapability.SendsPersonalNotification,
        typeof(EmptyConfig),
        ChannelOnly = true,
        Creatable = false,
        Label = "Email",
        Description = "Send alert emails via the platform's configured SMTP/Resend setup.",
        IconifyIcon = "logos:google-gmail"
    )]
    Email = 2,

    /// <summary>
    /// Not supported for now — DispatchPersonalAsync was never implemented. Kept in place (not
    /// removed/reordered) so its ordinal value doesn't shift and corrupt any existing DB rows.
    /// </summary>
    [Obsolete("Not supported for now.")]
    Webhook = 3,

    /// <summary>
    /// Not supported for now — DispatchPersonalAsync was never implemented. Kept in place (not
    /// removed/reordered) so its ordinal value doesn't shift and corrupt any existing DB rows.
    /// </summary>
    [Obsolete("Not supported for now.")]
    Slack = 4,

    /// <summary>
    /// Not supported for now — no notification dispatcher exists for this type (it would never
    /// send). Kept in place (not removed/reordered) so its ordinal value doesn't shift and corrupt
    /// any existing DB rows. See RFC 0004 for the planned implementation.
    /// </summary>
    [Obsolete("Not supported for now.")]
    PagerDuty = 5,

    [IntegrationManifest(
        IntegrationCategory.ThirdParty,
        IntegrationDirection.Inbound,
        IntegrationCapability.CreatesAlerts | IntegrationCapability.SupportsEscalationPolicy,
        typeof(GcpCloudMonitoringWebhookConfig),
        Label = "GCP Cloud Monitoring",
        Description = "Receive alerting policy notifications from Google Cloud Monitoring as Alerts.",
        IconifyIcon = "logos:google-cloud",
        WebhookPath = "gcp"
    )]
    GcpCloudMonitoringWebhook = 6,

    /// <summary>
    /// Not supported for now — its dispatcher's DispatchPersonalAsync is a stub (always returns
    /// false). Kept in place (not removed/reordered) so its ordinal value doesn't shift and corrupt
    /// any existing DB rows.
    /// </summary>
    [Obsolete("Not supported for now.")]
    MSTeams = 7,

    [IntegrationManifest(
        IntegrationCategory.Notification,
        IntegrationDirection.Outbound,
        IntegrationCapability.SendsPersonalNotification,
        typeof(TelegramConfig),
        Label = "Telegram",
        Description = "Notify a Telegram bot chat when alerts fire.",
        IconifyIcon = "logos:telegram"
    )]
    Telegram = 8,

    [IntegrationManifest(
        IntegrationCategory.Notification,
        IntegrationDirection.Outbound,
        IntegrationCapability.SendsPersonalNotification,
        typeof(TwilioConfig),
        Label = "Twilio",
        Description = "Send SMS alerts through Twilio.",
        IconifyIcon = "logos:twilio-icon"
    )]
    Twilio = 9,

    /// <summary>
    /// Not supported for now — DispatchPersonalAsync was never implemented. Kept in place (not
    /// removed/reordered) so its ordinal value doesn't shift and corrupt any existing DB rows.
    /// </summary>
    [Obsolete("Not supported for now.")]
    GoogleChat = 10,

    /// <summary>
    /// Not supported for now — DispatchPersonalAsync was never implemented. Kept in place (not
    /// removed/reordered) so its ordinal value doesn't shift and corrupt any existing DB rows.
    /// </summary>
    [Obsolete("Not supported for now.")]
    Discord = 11,

    /// <summary>
    /// Not supported for now — its dispatcher's DispatchPersonalAsync is a stub (always returns
    /// false). Kept in place (not removed/reordered) so its ordinal value doesn't shift and corrupt
    /// any existing DB rows.
    /// </summary>
    [Obsolete("Not supported for now.")]
    Opsgenie = 12,

    /// <summary>
    /// Not supported for now — its dispatcher only partially implements the contract
    /// (SendPersonalMessageAsync is a stub, so verification codes can't be delivered). Kept in place
    /// (not removed/reordered) so its ordinal value doesn't shift and corrupt any existing DB rows.
    /// </summary>
    [Obsolete("Not supported for now.")]
    Pushover = 13,

    [IntegrationManifest(
        IntegrationCategory.Notification,
        IntegrationDirection.Outbound,
        IntegrationCapability.SendsPersonalNotification,
        typeof(NtfyConfig),
        Label = "Ntfy",
        Description = "Publish alert notifications to a self-hosted or public ntfy topic.",
        IconifyIcon = "simple-icons:ntfy"
    )]
    Ntfy = 14,
}

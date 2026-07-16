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

    [IntegrationManifest(
        IntegrationCategory.Notification,
        IntegrationDirection.Outbound,
        IntegrationCapability.None,
        typeof(PagerDutyConfig),
        Label = "PagerDuty",
        Description = "Page your on-call team through PagerDuty.",
        IconifyIcon = "logos:pagerduty"
    )]
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

    [IntegrationManifest(
        IntegrationCategory.Notification,
        IntegrationDirection.Outbound,
        IntegrationCapability.SendsPersonalNotification,
        typeof(MSTeamsConfig),
        Label = "Microsoft Teams",
        Description = "Post alert notifications to a Microsoft Teams channel.",
        IconifyIcon = "logos:microsoft-teams"
    )]
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

    [IntegrationManifest(
        IntegrationCategory.Notification,
        IntegrationDirection.Outbound,
        IntegrationCapability.SendsPersonalNotification,
        typeof(OpsgenieConfig),
        Label = "Opsgenie",
        Description = "Route alerts into Opsgenie's on-call escalation.",
        IconifyIcon = "simple-icons:opsgenie"
    )]
    Opsgenie = 12,

    [IntegrationManifest(
        IntegrationCategory.Notification,
        IntegrationDirection.Outbound,
        IntegrationCapability.SendsPersonalNotification,
        typeof(PushoverConfig),
        Label = "Pushover",
        Description = "Send push notifications via Pushover.",
        IconifyIcon = "tabler:brand-pushover"
    )]
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

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
        IntegrationCapability.RequiresOAuthConnection | IntegrationCapability.ProvidesActions,
        typeof(JiraConfig),
        Label = "Jira",
        Description = "Create and track Jira tickets from alerts, incidents, and maintenances.",
        IconifyIcon = "logos:jira"
    )]
    [IntegrationAction(
        "create-issue",
        "Create Jira ticket",
        new[] { ActionContext.Alert, ActionContext.Incident, ActionContext.Maintenance },
        Description = "Create a Jira ticket and link it back to this object.",
        IconifyIcon = "logos:jira",
        HasInput = true,
        SupportsDraft = true
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
    /// Generic outbound webhook (RFC 0015) — POSTs a fixed, versioned JSON payload to any URL when a
    /// subscribed alert or incident event fires. Compatible with Zapier/Make catch-hooks. A
    /// channel-target dispatcher: the destination is the config URL, not a per-person handle.
    /// </summary>
    [IntegrationManifest(
        IntegrationCategory.Notification,
        IntegrationDirection.Outbound,
        IntegrationCapability.SendsChannelNotification,
        typeof(WebhookConfig),
        Label = "Webhook",
        Description = "POST a JSON payload to any URL when an incident or alert event fires (Zapier/Make compatible).",
        IconifyIcon = "tabler:webhook"
    )]
    Webhook = 3,

    /// <summary>
    /// Not supported for now — DispatchPersonalAsync was never implemented. Kept in place (not
    /// removed/reordered) so its ordinal value doesn't shift and corrupt any existing DB rows.
    /// </summary>
    [Obsolete("Not supported for now.")]
    Slack = 4,

    /// <summary>
    /// PagerDuty (RFC 0004). Phase 1: the integration is OAuth-connectable — the admin connects Piro
    /// to their PagerDuty account so services and routing keys can later be discovered. The alert
    /// dispatcher itself (SendsAlertEvents) is a later phase, so the manifest declares only
    /// RequiresOAuthConnection today — an honest statement of what actually works.
    /// </summary>
    [IntegrationManifest(
        IntegrationCategory.ThirdParty,
        IntegrationDirection.Outbound,
        IntegrationCapability.RequiresOAuthConnection | IntegrationCapability.SendsAlertEvents,
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

    [IntegrationManifest(
        IntegrationCategory.Notification,
        IntegrationDirection.Outbound,
        IntegrationCapability.SendsChannelNotification,
        typeof(GoogleChatConfig),
        Label = "Google Chat",
        Description = "Post alert notifications to a Google Chat space via an incoming webhook.",
        IconifyIcon = "logos:google-meet"
    )]
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

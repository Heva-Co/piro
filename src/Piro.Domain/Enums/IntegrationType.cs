namespace Piro.Domain.Enums;

/// <summary>
/// DEPRECATED (RFC 0016): the closed integration discriminator. Its manifests moved onto the
/// per-integration <c>IIntegration</c> classes, and its role as the dispatch/config discriminator is
/// being replaced by the open <c>string IntegrationId</c>. This enum is retained transiently only
/// while remaining call sites migrate to the string id; it carries no manifest attributes anymore.
/// The persisted value in every <c>Integration</c> row is the member name string, so removing the
/// enum requires no data migration.
/// </summary>
public enum IntegrationType
{
    GoogleCloud = 0,
    Jira = 1,
    Email = 2,
    Webhook = 3,
    [Obsolete("Not supported for now.")]
    Slack = 4,
    PagerDuty = 5,
    GcpCloudMonitoringWebhook = 6,
    [Obsolete("Not supported for now.")]
    MSTeams = 7,
    Telegram = 8,
    Twilio = 9,
    GoogleChat = 10,
    [Obsolete("Not supported for now.")]
    Discord = 11,
    [Obsolete("Not supported for now.")]
    Opsgenie = 12,
    [Obsolete("Not supported for now.")]
    Pushover = 13,
    Ntfy = 14,
}

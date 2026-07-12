using Piro.Domain.Attributes;

namespace Piro.Domain.Enums;

public enum IntegrationType
{
    // Third-party integrations
    [IntegrationCategory(IntegrationCategory.ThirdParty)] 
    GoogleCloud,
    [IntegrationCategory(IntegrationCategory.ThirdParty)] 
    Jira,

    // Notification integrations (formerly NotificationChannelType)
    [IntegrationCategory(IntegrationCategory.Notification, ChannelOnly = true)]
    Email,

    /// <summary>Not supported for now — DispatchPersonalAsync was never implemented. Kept in place (not removed/reordered) so its ordinal value doesn't shift and corrupt any existing DB rows.</summary>
    [Obsolete("Not supported for now.")]
    [IntegrationCategory(IntegrationCategory.Notification, ChannelOnly = true)]
    Webhook,

    /// <summary>Not supported for now — DispatchPersonalAsync was never implemented. Kept in place (not removed/reordered) so its ordinal value doesn't shift and corrupt any existing DB rows.</summary>
    [Obsolete("Not supported for now.")]
    [IntegrationCategory(IntegrationCategory.Notification, ChannelOnly = true)]
    Slack,

    [IntegrationCategory(IntegrationCategory.Notification)]
    PagerDuty,

    [IntegrationCategory(IntegrationCategory.Notification)]
    MSTeams,

    [IntegrationCategory(IntegrationCategory.Notification)]
    Telegram,

    [IntegrationCategory(IntegrationCategory.Notification)]
    Twilio,

    /// <summary>Not supported for now — DispatchPersonalAsync was never implemented. Kept in place (not removed/reordered) so its ordinal value doesn't shift and corrupt any existing DB rows.</summary>
    [Obsolete("Not supported for now.")]
    [IntegrationCategory(IntegrationCategory.Notification, ChannelOnly = true)]
    GoogleChat,

    /// <summary>Not supported for now — DispatchPersonalAsync was never implemented. Kept in place (not removed/reordered) so its ordinal value doesn't shift and corrupt any existing DB rows.</summary>
    [Obsolete("Not supported for now.")]
    [IntegrationCategory(IntegrationCategory.Notification, ChannelOnly = true)]
    Discord,

    [IntegrationCategory(IntegrationCategory.Notification)]
    Opsgenie,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    Pushover,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    Ntfy,
}

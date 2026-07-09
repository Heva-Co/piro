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

    [IntegrationCategory(IntegrationCategory.Notification, ChannelOnly = true)]
    Webhook,

    [IntegrationCategory(IntegrationCategory.Notification, ChannelOnly = true)]
    Slack,

    [IntegrationCategory(IntegrationCategory.Notification)]
    PagerDuty,

    [IntegrationCategory(IntegrationCategory.Notification)]
    MSTeams,

    [IntegrationCategory(IntegrationCategory.Notification)]
    Telegram,

    [IntegrationCategory(IntegrationCategory.Notification)]
    TwilioSms,

    [IntegrationCategory(IntegrationCategory.Notification, ChannelOnly = true)]
    GoogleChat,

    [IntegrationCategory(IntegrationCategory.Notification, ChannelOnly = true)]
    Discord,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    Opsgenie,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    Pushover,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    Ntfy,
}

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
    [IntegrationCategory(IntegrationCategory.Notification)] 
    Email,
    
    [IntegrationCategory(IntegrationCategory.Notification)] 
    Webhook,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    Slack,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    PagerDuty,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    MSTeams,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    Telegram,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    TwilioSms,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    GoogleChat,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    Discord,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    Opsgenie,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    Pushover,

    [IntegrationCategory(IntegrationCategory.Notification)] 
    Ntfy,
}

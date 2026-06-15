namespace Piro.Domain.Enums;

/// <summary>Notification channel used by a trigger.</summary>
public enum TriggerType
{
    Email,
    Webhook,
    Slack,
    PagerDuty,
    MSTeams,
    Telegram,
    TwilioSms,
    GoogleChat
}

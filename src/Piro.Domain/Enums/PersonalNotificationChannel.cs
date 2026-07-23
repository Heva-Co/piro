using Piro.Domain.Attributes;
using Piro.Contracts;

namespace Piro.Domain.Enums;

/// <summary>
/// Channels a user can pick for their personal on-call notification preferences (Profile >
/// Notification preferences). Deliberately a separate enum from <see cref="IntegrationType"/>:
/// most integration types (Webhook, Slack, Discord, PagerDuty, etc.) post to a shared team
/// channel and aren't meaningful as "notify me personally" options, so only the handful of
/// channels that identify one specific person are listed here.
/// </summary>
public enum PersonalNotificationChannel
{
    /// <summary>Self-sufficient from the user's own address — no platform Integration needed.</summary>
    [PersonalChannel(RequiresIntegration = false, IntegrationType = IntegrationType.Email)]
    Email,

    [PersonalChannel(RequiresIntegration = true, IntegrationType = IntegrationType.Telegram)]
    Telegram,

    [PersonalChannel(RequiresIntegration = true, IntegrationType = IntegrationType.Twilio)]
    TwilioSms,

    [PersonalChannel(RequiresIntegration = true, IntegrationType = IntegrationType.Ntfy)]
    Ntfy,
}

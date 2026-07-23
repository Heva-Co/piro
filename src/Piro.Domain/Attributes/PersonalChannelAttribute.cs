namespace Piro.Domain.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public sealed class PersonalChannelAttribute : Attribute
{
    /// <summary>
    /// When true, dispatching this channel requires shared platform credentials, so a
    /// UserNotificationPreference of this channel must also reference an Integration. When
    /// false, the user's own Handle alone (an address, chat id, phone number, etc.) is
    /// self-sufficient — no platform Integration row is needed.
    /// </summary>
    public bool RequiresIntegration { get; init; }

    /// <summary>
    /// The integration id (RFC 0016) this channel's credentials come from when
    /// <see cref="RequiresIntegration"/> is true (e.g. Telegram → "Telegram"). Empty when
    /// RequiresIntegration is false.
    /// </summary>
    public string IntegrationId { get; init; } = string.Empty;
}

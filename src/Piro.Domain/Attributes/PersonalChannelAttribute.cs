using Piro.Domain.Enums;

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
    /// The platform IntegrationType this channel's credentials come from when
    /// <see cref="RequiresIntegration"/> is true (e.g. Telegram → IntegrationType.Telegram).
    /// Unused when RequiresIntegration is false.
    /// </summary>
    public IntegrationType IntegrationType { get; init; }
}

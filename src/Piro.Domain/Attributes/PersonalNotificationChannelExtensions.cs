using System.Reflection;
using Piro.Domain.Enums;

namespace Piro.Domain.Attributes;

public static class PersonalNotificationChannelExtensions
{
    private static PersonalChannelAttribute? GetAttribute(PersonalNotificationChannel channel) =>
        typeof(PersonalNotificationChannel)
            .GetField(channel.ToString())
            ?.GetCustomAttribute<PersonalChannelAttribute>();

    public static bool RequiresIntegration(this PersonalNotificationChannel channel) =>
        GetAttribute(channel)?.RequiresIntegration ?? false;

    /// <summary>The INotificationDispatcher.Type this channel dispatches through (used to look up the right dispatcher).</summary>
    public static IntegrationType ToIntegrationType(this PersonalNotificationChannel channel) =>
        GetAttribute(channel)?.IntegrationType
            ?? throw new InvalidOperationException($"Personal channel '{channel}' has no [PersonalChannel] attribute.");
}

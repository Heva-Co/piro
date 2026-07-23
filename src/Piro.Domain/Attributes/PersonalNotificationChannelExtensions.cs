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

    /// <summary>The integration id (RFC 0016) this channel dispatches through — used to look up the right INotificationDispatcher.</summary>
    public static string ToIntegrationId(this PersonalNotificationChannel channel) =>
        GetAttribute(channel) is { IntegrationId.Length: > 0 } attr
            ? attr.IntegrationId
            : throw new InvalidOperationException($"Personal channel '{channel}' has no [PersonalChannel] integration id.");
}

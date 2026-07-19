using System.Text.Json;
using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class NotificationSubscriptionExtensions
{
    public static NotificationSubscriptionDto ToDto(this NotificationSubscription s) => new(
        s.Id,
        s.Name,
        DeserializeEvents(s.EventsJson),
        s.MinSeverity,
        s.TargetKind,
        s.UserId,
        s.User?.UserName ?? s.User?.Email,
        s.IntegrationId,
        s.Integration?.Name,
        s.Target,
        s.Enabled);

    /// <summary>The subscription's event wire names, parsed from its JSON storage. Empty on malformed JSON.</summary>
    public static IReadOnlyList<string> Events(this NotificationSubscription s) => DeserializeEvents(s.EventsJson);

    private static IReadOnlyList<string> DeserializeEvents(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}

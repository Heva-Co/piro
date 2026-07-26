using System.Text.Json;
using Piro.Application.DTOs;
using Piro.Domain.Entities;
using Piro.Domain.Tags;

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
        s.Enabled,
        s.Filter());

    /// <summary>The subscription's event wire names, parsed from its JSON storage. Empty on malformed JSON.</summary>
    public static IReadOnlyList<string> Events(this NotificationSubscription s) => DeserializeEvents(s.EventsJson);

    /// <summary>
    /// The subscription's tag filter, deserialized from <see cref="NotificationSubscription.FilterJson"/>.
    /// Null when there is no filter or the stored JSON is unparseable — either way the subscription is
    /// treated as having no tag filter, so a bad row degrades gracefully instead of crashing the worker.
    /// </summary>
    public static TagSelector? Filter(this NotificationSubscription s)
    {
        if (string.IsNullOrWhiteSpace(s.FilterJson)) return null;
        try { return JsonSerializer.Deserialize<TagSelector>(s.FilterJson); }
        catch { return null; }
    }

    private static IReadOnlyList<string> DeserializeEvents(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}

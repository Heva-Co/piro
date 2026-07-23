using System.Reflection;
using Piro.Domain.Attributes;
using Piro.Contracts;
using Piro.Domain.Enums;

namespace Piro.Domain.Extensions;

/// <summary>
/// Reflection helpers over the <see cref="NotificationEventType"/> catalog (RFC 0009 §4.2). The
/// <see cref="NotificationEventAttribute"/> on each enum value is the single source of truth for its
/// wire name and description; these helpers read it. Mirrors <c>CheckTypeExtensions.GetManifest</c>.
/// </summary>
public static class NotificationEventTypeExtensions
{
    // wire name -> enum value, built once from the attributes. The catalog is closed, so this is safe
    // to cache; a duplicate wire name is a developer error and surfaces here as a build/startup throw.
    private static readonly IReadOnlyDictionary<string, NotificationEventType> ByWireName =
        Enum.GetValues<NotificationEventType>()
            .ToDictionary(e => e.WireName(), e => e);

    /// <summary>
    /// Returns this event's declared <see cref="NotificationEventAttribute"/>. Throws if a catalog
    /// value is missing its attribute — every value must be annotated (guarded by tests).
    /// </summary>
    public static NotificationEventAttribute GetManifest(this NotificationEventType type) =>
        typeof(NotificationEventType)
            .GetField(type.ToString())
            ?.GetCustomAttribute<NotificationEventAttribute>()
        ?? throw new InvalidOperationException(
            $"NotificationEventType.{type} is missing its [NotificationEvent] attribute.");

    /// <summary>The stable wire name (e.g. <c>alert:created</c>) referenced from subscriptions and payloads.</summary>
    public static string WireName(this NotificationEventType type) => type.GetManifest().Name;

    /// <summary>The human-readable description of when this event fires.</summary>
    public static string Description(this NotificationEventType type) => type.GetManifest().Description;

    /// <summary>
    /// Resolves a wire name back to its catalog value, or null if it is not a known event. Used when
    /// deserializing an event whose type arrives as its wire-name string.
    /// </summary>
    public static NotificationEventType? FromWireName(string wireName) =>
        ByWireName.TryGetValue(wireName, out var type) ? type : null;
}

using System.Reflection;
using FluentAssertions;
using Piro.Application.Models.NotificationEvents;
using Piro.Domain.Attributes;
using Piro.Contracts;
using Piro.Domain.Enums;
using Piro.Domain.Extensions;

namespace Piro.UnitTests;

/// <summary>
/// Guards the notification event catalog and its payload contracts (RFC 0009 §4.2, §4.3).
/// <para>
/// The payload shape check is a <b>compatibility guard</b>: it freezes each event payload's field set
/// (name → type) as a golden snapshot. Adding an optional field is a deliberate one-line update here;
/// renaming, retyping, or removing a field fails the test — enforcing the "additive only" rule so the
/// public contract can't be broken by a distracted PR. When you intentionally evolve a payload
/// (add a field and bump its Version), update the frozen shape below in the same commit.
/// </para>
/// </summary>
public class NotificationEventCatalogTests
{
    [Fact]
    public void EveryCatalogValue_HasANotificationEventAttribute()
    {
        foreach (var type in Enum.GetValues<NotificationEventType>())
            type.GetManifest().Should().NotBeNull($"{type} must declare a [NotificationEvent]");
    }

    [Fact]
    public void EveryWireName_IsUnique()
    {
        var names = Enum.GetValues<NotificationEventType>().Select(t => t.WireName()).ToList();
        names.Should().OnlyHaveUniqueItems("catalog wire names are stable identifiers and must not collide");
    }

    [Theory]
    [InlineData(NotificationEventType.AlertCreated, "alert:created")]
    [InlineData(NotificationEventType.AlertAcknowledged, "alert:acknowledged")]
    [InlineData(NotificationEventType.AlertResolved, "alert:resolved")]
    [InlineData(NotificationEventType.IncidentCreated, "incident:created")]
    [InlineData(NotificationEventType.IncidentResolved, "incident:resolved")]
    [InlineData(NotificationEventType.SystemIntegrationExpired, "system:integration:expired")]
    public void WireName_IsFrozen(NotificationEventType type, string expected)
    {
        // Wire names are permanent — this pins them so a rename is caught in review.
        type.WireName().Should().Be(expected);
    }

    [Fact]
    public void EveryWireName_FollowsDomainVerbShape()
    {
        foreach (var type in Enum.GetValues<NotificationEventType>())
        {
            var segments = type.WireName().Split(':');
            segments.Length.Should().BeGreaterThanOrEqualTo(2, $"{type} wire name must be domain:...:verb");
            segments.Should().OnlyContain(s => s.Length > 0, $"{type} wire name must have no empty segments");
        }
    }

    [Fact]
    public void FromWireName_RoundTripsEveryCatalogValue()
    {
        foreach (var type in Enum.GetValues<NotificationEventType>())
            NotificationEventTypeExtensions.FromWireName(type.WireName()).Should().Be(type);
    }

    [Fact]
    public void FromWireName_ReturnsNull_ForUnknownEvent()
    {
        NotificationEventTypeExtensions.FromWireName("not:a:real:event").Should().BeNull();
    }

    /// <summary>
    /// The frozen contract: each payload type's field set. Keyed by wire name. A field is
    /// "PropertyName:TypeName". Update deliberately (and bump the payload's Version) when adding fields.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> FrozenPayloadShapes = new Dictionary<string, string[]>
    {
        ["alert:created"] =
        [
            "AlertId:Int32", "ServiceName:String", "CheckName:String", "Severity:AlertSeverity",
            "Tags:IReadOnlyList`1", "IsExternal:Boolean", "SourceLabel:String", "FiredAt:DateTimeOffset",
            "ServiceId:Int32", "EventType:String", "Version:Int32",
        ],
        ["alert:acknowledged"] =
        [
            "AlertId:Int32", "ServiceName:String", "CheckName:String", "Severity:AlertSeverity",
            "Tags:IReadOnlyList`1", "AcknowledgedBy:String", "AcknowledgedAt:DateTimeOffset",
            "ServiceId:Int32", "EventType:String", "Version:Int32",
        ],
        ["alert:resolved"] =
        [
            "AlertId:Int32", "ServiceName:String", "CheckName:String", "Severity:AlertSeverity",
            "Tags:IReadOnlyList`1", "ResolvedAt:DateTimeOffset",
            "ServiceId:Int32", "EventType:String", "Version:Int32",
        ],
        ["incident:created"] =
        [
            "IncidentId:Int32", "Title:String", "Status:IncidentStatus", "Visibility:IncidentVisibility",
            "AffectedServices:IReadOnlyList`1", "CreatedAt:DateTimeOffset",
            "EventType:String", "Version:Int32",
        ],
        ["incident:resolved"] =
        [
            "IncidentId:Int32", "Title:String", "Status:IncidentStatus", "Visibility:IncidentVisibility",
            "AffectedServices:IReadOnlyList`1", "ResolvedAt:DateTimeOffset",
            "EventType:String", "Version:Int32",
        ],
        ["system:integration:expired"] =
        [
            "IntegrationId:Guid", "IntegrationName:String", "Type:IntegrationType", "Reason:String",
            "ExpiredAt:DateTimeOffset",
            "EventType:String", "Version:Int32",
        ],
    };

    public static IEnumerable<object[]> PayloadTypes()
    {
        foreach (var t in typeof(INotificationEvent).Assembly.GetTypes()
                     .Where(t => typeof(INotificationEvent).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false }))
            yield return [t];
    }

    [Theory]
    [MemberData(nameof(PayloadTypes))]
    public void EveryPayload_MatchesItsFrozenContractShape(Type payloadType)
    {
        var instance = (INotificationEvent)CreateSample(payloadType);

        FrozenPayloadShapes.Should().ContainKey(instance.EventType,
            $"{payloadType.Name} exposes wire name '{instance.EventType}' with no frozen contract — add one to FrozenPayloadShapes");

        var actualShape = payloadType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => $"{p.Name}:{FriendlyTypeName(p.PropertyType)}")
            .ToArray();

        actualShape.Should().BeEquivalentTo(FrozenPayloadShapes[instance.EventType],
            $"the '{instance.EventType}' contract is frozen — a mismatch means a field was renamed, retyped, " +
            "or removed (breaking) or added without updating the frozen shape (bump Version + update here)");
    }

    [Theory]
    [MemberData(nameof(PayloadTypes))]
    public void EveryPayload_WireNameIsAKnownCatalogEvent(Type payloadType)
    {
        var instance = (INotificationEvent)CreateSample(payloadType);
        NotificationEventTypeExtensions.FromWireName(instance.EventType).Should()
            .NotBeNull($"{payloadType.Name}.EventType '{instance.EventType}' must be a catalog value");
    }

    // Builds a payload with default-ish arguments purely to read its EventType/Version and shape.
    private static object CreateSample(Type payloadType)
    {
        var ctor = payloadType.GetConstructors().Single();
        var args = ctor.GetParameters().Select(p => DefaultArg(p.ParameterType)).ToArray();
        return ctor.Invoke(args);
    }

    private static object? DefaultArg(Type t)
    {
        if (t == typeof(string)) return string.Empty;
        if (t == typeof(Guid)) return Guid.Empty;
        if (t == typeof(DateTimeOffset)) return default(DateTimeOffset);
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(t) && t != typeof(string))
            return Array.Empty<string>();
        return t.IsValueType ? Activator.CreateInstance(t) : null;
    }

    private static string FriendlyTypeName(Type t) =>
        Nullable.GetUnderlyingType(t)?.Name ?? t.Name;
}

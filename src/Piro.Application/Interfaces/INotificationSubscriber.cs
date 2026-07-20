using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>
/// What an integration <b>declares</b> it can consume from the event catalog (RFC 0009 §4.4): the
/// menu the admin orders from. The integration limits which events it can handle and in which delivery
/// mode; the admin then activates which of those actually fire, to which destination, via a
/// <c>NotificationSubscription</c>. Keeping this a stable code contract (not a runtime list) is what
/// lets integrations later move out into independent plugins.
/// </summary>
public interface INotificationSubscriber
{
    /// <summary>The integration type this subscriber speaks for.</summary>
    IntegrationType Type { get; }

    /// <summary>The catalog events this type can meaningfully handle — the subset offered in the UI.</summary>
    IReadOnlySet<NotificationEventType> SupportedEvents { get; }

    /// <summary>Which delivery contract this type uses (personal / group / integration).</summary>
    NotificationTargetKind TargetKind { get; }
}

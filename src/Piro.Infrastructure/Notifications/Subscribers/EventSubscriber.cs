using Piro.Application.Interfaces;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Notifications.Subscribers;

/// <summary>
/// Declares which catalog events an integration type consumes (RFC 0009 §4.4) and in which delivery
/// mode. Registered once per (type, mode, event-set) rather than duplicating a near-identical class per
/// provider. The event set is the menu the admin's subscription UI offers for that destination;
/// <see cref="TargetKind"/> tells the wizard whether it is a person, a team channel, or an integration.
/// </summary>
internal sealed class EventSubscriber(
    IntegrationType type,
    NotificationTargetKind targetKind,
    IReadOnlySet<NotificationEventType> supportedEvents) : INotificationSubscriber
{
    public IntegrationType Type => type;
    public IReadOnlySet<NotificationEventType> SupportedEvents => supportedEvents;
    public NotificationTargetKind TargetKind => targetKind;

    /// <summary>The alert lifecycle events (created/acknowledged/resolved).</summary>
    public static readonly IReadOnlySet<NotificationEventType> AlertEvents = new HashSet<NotificationEventType>
    {
        NotificationEventType.AlertCreated,
        NotificationEventType.AlertAcknowledged,
        NotificationEventType.AlertResolved,
    };

    /// <summary>The alert and incident lifecycle events — for channels that carry both.</summary>
    public static readonly IReadOnlySet<NotificationEventType> AlertAndIncidentEvents = new HashSet<NotificationEventType>
    {
        NotificationEventType.AlertCreated,
        NotificationEventType.AlertAcknowledged,
        NotificationEventType.AlertResolved,
        NotificationEventType.IncidentCreated,
        NotificationEventType.IncidentResolved,
    };
}

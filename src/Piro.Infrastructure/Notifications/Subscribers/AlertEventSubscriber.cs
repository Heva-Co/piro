using Piro.Application.Interfaces;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Notifications.Subscribers;

/// <summary>
/// Declares that an integration type consumes the alert lifecycle events (RFC 0009 §4.4) in a given
/// delivery mode. All alert-capable channels support the same alert events in v1, so this one class is
/// registered once per (type, mode) rather than duplicating a near-identical class per provider. The
/// event set is the menu the admin's subscription UI offers; <see cref="TargetKind"/> tells the wizard
/// whether the destination is a person, a team channel, or an external integration.
/// </summary>
internal sealed class AlertEventSubscriber(IntegrationType type, NotificationTargetKind targetKind) : INotificationSubscriber
{
    public IntegrationType Type => type;

    public IReadOnlySet<NotificationEventType> SupportedEvents { get; } = new HashSet<NotificationEventType>
    {
        NotificationEventType.AlertCreated,
        NotificationEventType.AlertAcknowledged,
        NotificationEventType.AlertResolved,
    };

    public NotificationTargetKind TargetKind => targetKind;
}

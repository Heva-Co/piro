using Piro.Application.Interfaces;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Notifications.Subscribers;

/// <summary>
/// Declares that a personal-notification integration type consumes the alert lifecycle events
/// (RFC 0009 §4.4). All personal alert channels support the same alert events in v1, so this one class
/// is registered once per type rather than duplicating a near-identical class per provider. The set is
/// the menu the admin's subscription UI offers for that destination; <see cref="NotificationTargetKind.Personal"/>
/// is what makes the wizard skip the person-vs-group sub-step.
/// </summary>
internal sealed class PersonalAlertSubscriber(IntegrationType type) : INotificationSubscriber
{
    public IntegrationType Type => type;

    public IReadOnlySet<NotificationEventType> SupportedEvents { get; } = new HashSet<NotificationEventType>
    {
        NotificationEventType.AlertCreated,
        NotificationEventType.AlertAcknowledged,
        NotificationEventType.AlertResolved,
    };

    public NotificationTargetKind TargetKind => NotificationTargetKind.Personal;
}

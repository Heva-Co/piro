using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>
/// Domain-facing publisher for alert lifecycle events (RFC 0009 §4.2). A call site passes only the
/// alert and which catalog event happened — never a payload. The contracted payload (wire name,
/// version, shape) is built internally from the alert's own data <em>as it is at the moment of the
/// call</em>, with no database reload, so a concurrent edit can't retroactively change an
/// already-emitted event.
/// <para>
/// The alert must be passed with the navigations the snapshot needs already loaded —
/// <see cref="Alert.Service"/>, <see cref="Alert.Check"/>, and <see cref="Alert.AlertConfig"/> (for
/// severity). The call site is the right place to guarantee that: it is at the event's moment with the
/// live entity.
/// </para>
/// </summary>
public interface IAlertNotificationPublisher
{
    /// <summary>
    /// Publishes an alert lifecycle event. <paramref name="evt"/> must be one of the alert catalog
    /// values (<see cref="NotificationEventType.AlertCreated"/>, <see cref="NotificationEventType.AlertAcknowledged"/>,
    /// <see cref="NotificationEventType.AlertResolved"/>).
    /// </summary>
    Task PublishAsync(Alert alert, NotificationEventType evt, CancellationToken ct = default);
}

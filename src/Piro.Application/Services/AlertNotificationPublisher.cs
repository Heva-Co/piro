using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Application.Models.NotificationEvents;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Maps an alert lifecycle event to its contracted payload (RFC 0009 §4.2/§4.3) and publishes it. The
/// payload contract lives entirely here — call sites pass only the alert and the event. Each field is
/// read from the alert's loaded navigations (via <see cref="AlertExtensions"/>) and copied into the
/// immutable payload record synchronously, so the snapshot reflects the moment of the call and no
/// later mutation or reload can change it.
/// </summary>
public class AlertNotificationPublisher(INotificationEventPublisher publisher) : IAlertNotificationPublisher
{
    public Task PublishAsync(Alert alert, NotificationEventType evt, CancellationToken ct = default)
    {
        INotificationEvent payload = evt switch
        {
            NotificationEventType.AlertCreated => new AlertCreatedPayload(
                alert.Id,
                alert.ServiceLabel(),
                alert.CheckLabel(),
                alert.SeverityOrDefault(),
                Tags: [],
                alert.IsExternal(),
                alert.ExternalSourceLabel(),
                alert.FiredAt,
                ServiceId: alert.ServiceId
            ),

            NotificationEventType.AlertAcknowledged => new AlertAcknowledgedPayload(
                alert.Id,
                alert.ServiceLabel(),
                alert.CheckLabel(),
                alert.SeverityOrDefault(),
                Tags: [],
                alert.AcknowledgedBy,
                DateTimeOffset.UtcNow,
                ServiceId: alert.ServiceId
            ),

            NotificationEventType.AlertResolved => new AlertResolvedPayload(
                alert.Id,
                alert.ServiceLabel(),
                alert.CheckLabel(),
                alert.SeverityOrDefault(),
                Tags: [],
                alert.ResolvedAt ?? DateTimeOffset.UtcNow,
                ServiceId: alert.ServiceId
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(evt), evt, "Not an alert lifecycle event."),
        };

        // All events of one alert share an ordering key so they reach each destination in emit order.
        return publisher.PublishAsync(payload, $"alert:{alert.Id}", ct);
    }
}

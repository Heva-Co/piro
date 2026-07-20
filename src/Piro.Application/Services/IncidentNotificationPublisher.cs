using Piro.Application.Interfaces;
using Piro.Application.Models.NotificationEvents;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Maps an incident lifecycle event to its contracted payload (RFC 0009 §4.2/§4.3) and publishes it.
/// The payload contract lives here; the snapshot (title, status, affected services, visibility) is read
/// from the incident's loaded navigations synchronously, so it reflects the moment of the call.
/// </summary>
public class IncidentNotificationPublisher(INotificationEventPublisher publisher) : IIncidentNotificationPublisher
{
    public Task PublishAsync(Incident incident, NotificationEventType evt, CancellationToken ct = default)
    {
        var services = incident.IncidentServices
            .Select(s => s.Service?.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToList();

        INotificationEvent payload = evt switch
        {
            NotificationEventType.IncidentCreated => new IncidentCreatedPayload(
                incident.Id, incident.Title, incident.Status, incident.Visibility,
                services, DateTimeOffset.UtcNow),

            NotificationEventType.IncidentResolved => new IncidentResolvedPayload(
                incident.Id, incident.Title, incident.Status, incident.Visibility,
                services, DateTimeOffset.UtcNow),

            _ => throw new ArgumentOutOfRangeException(nameof(evt), evt, "Not an incident lifecycle event."),
        };

        // All events of one incident share an ordering key so they reach each destination in emit order.
        return publisher.PublishAsync(payload, $"incident:{incident.Id}", ct);
    }
}

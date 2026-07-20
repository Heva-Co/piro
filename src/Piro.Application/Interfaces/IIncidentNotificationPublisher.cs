using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>
/// Domain-facing publisher for incident lifecycle events (RFC 0009 §4.2). A call site passes only the
/// incident and which catalog event happened — never a payload. The contracted payload is built
/// internally from the incident's own data as it is at the moment of the call, with no database reload.
/// <para>
/// The incident must be passed with its affected services loaded (<see cref="Incident.IncidentServices"/>
/// with their <c>Service</c>) so the snapshot lists them; the call site guarantees that at emit time.
/// </para>
/// </summary>
public interface IIncidentNotificationPublisher
{
    /// <summary>
    /// Publishes an incident lifecycle event. <paramref name="evt"/> must be one of the incident catalog
    /// values (<see cref="NotificationEventType.IncidentCreated"/>, <see cref="NotificationEventType.IncidentResolved"/>).
    /// </summary>
    Task PublishAsync(Incident incident, NotificationEventType evt, CancellationToken ct = default);
}

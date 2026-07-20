using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>
/// The seam between the durable push engine's transport (the outbox + <c>NotificationDispatchWorker</c>,
/// RFC 0009 §4.6) and what actually happens to an event: matching subscriptions, dispatching to
/// personal/group/integration destinations, and recording <see cref="NotificationDeliveryLog"/> rows.
/// The worker owns ordering, idempotency, and retry; the processor owns response. Splitting them keeps
/// the transport fully testable in isolation, and lets later phases fill in real routing behind a stable
/// contract (phase 3 ships a no-op processor).
/// </summary>
public interface INotificationEventProcessor
{
    /// <summary>
    /// Processes one drained outbox row. Implementations deserialize <see cref="NotificationEventOutbox.PayloadJson"/>
    /// by its <see cref="NotificationEventOutbox.EventType"/>, route it, and write delivery-log rows.
    /// A throw signals a transient failure — the worker records it and reschedules with backoff, or
    /// quarantines the row once its retry budget is exhausted. Returning normally marks the row Done.
    /// </summary>
    Task ProcessAsync(NotificationEventOutbox outboxRow, CancellationToken ct = default);
}

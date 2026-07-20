using Piro.Application.Models.NotificationEvents;

namespace Piro.Application.Interfaces;

/// <summary>
/// Entry point to the durable notification push engine (RFC 0009 §4.6): serializes an event and
/// writes it to the outbox as a single Pending row. This is the one integration point sources touch —
/// they publish a fact and nothing more; scheduling, ordering, retry, and routing are the engine's job.
/// </summary>
public interface INotificationEventPublisher
{
    /// <summary>
    /// Publishes <paramref name="evt"/> to the outbox. <paramref name="orderingKey"/> groups all events
    /// of one logical entity (e.g. <c>alert:4821</c>) so they are delivered in emit order per
    /// destination; events with different keys are independent. Returns the new outbox row id.
    /// </summary>
    Task<long> PublishAsync(INotificationEvent evt, string orderingKey, CancellationToken ct = default);
}

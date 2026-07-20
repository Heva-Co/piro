using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// A durable row in the notification push engine's outbox (RFC 0009 §4.6) — one queued <b>event</b>
/// (e.g. <c>alert:created</c>) awaiting processing, not a resolved notification: who is notified is
/// decided later, when the worker drains this row and matches it against subscriptions. The publisher
/// writes one row per emitted event; <c>NotificationDispatchWorker</c> drains it with three guarantees — ordering
/// per entity (via <see cref="OrderingKey"/> + the monotonic <see cref="Id"/>), effectively-once
/// idempotency (via the delivery ledger), and retry with backoff to a <see cref="OutboxStatus.Failed"/>
/// quarantine. A durable outbox (not an in-memory queue) is what lets a "your service is down"
/// notification survive a process restart.
/// </summary>
public class NotificationEventOutbox
{
    /// <summary>
    /// Auto-increment PK that doubles as the <b>global monotonic ordering sequence</b>: rows are
    /// assigned ids in publish order, so ordering never relies on wall-clock time (which can tie or
    /// jump). Within an <see cref="OrderingKey"/>, lower id = earlier event.
    /// </summary>
    public long Id { get; set; }

    /// <summary>The catalog wire name of the event (e.g. <c>alert:created</c>).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Groups all events of one logical entity (e.g. <c>alert:4821</c>) so the worker can deliver them
    /// in id order per destination. Events with different keys are independent and never block each other.
    /// </summary>
    public string OrderingKey { get; set; } = string.Empty;

    /// <summary>The serialized event payload (a frozen <c>INotificationEvent</c> snapshot).</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    /// <summary>How many times a worker has tried to process this row.</summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Earliest time this row may be (re)processed — the exponential-backoff schedule (UTC). Null means
    /// "as soon as possible". Also used as the lease deadline while <see cref="OutboxStatus.Processing"/>.
    /// </summary>
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>The last processing error, retained when the row is retried or quarantined.</summary>
    public string? LastError { get; set; }

    /// <summary>Set automatically by the DbContext audit hook (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the row reached a terminal state (<see cref="OutboxStatus.Done"/>/<see cref="OutboxStatus.Failed"/>), UTC.</summary>
    public DateTime? ProcessedAt { get; set; }
}

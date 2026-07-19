namespace Piro.Domain.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.NotificationEventOutbox"/> row as it moves through the durable push
/// engine (RFC 0009 §4.6). Persisted as a string so the values are stable and legible in the DB.
/// </summary>
public enum OutboxStatus
{
    /// <summary>Written by the publisher, not yet picked up. Eligible for the next drain.</summary>
    Pending,

    /// <summary>Claimed by a worker and being handled. Reclaimed to <see cref="Pending"/> if the lease expires.</summary>
    Processing,

    /// <summary>Handled successfully. Terminal.</summary>
    Done,

    /// <summary>Exhausted its retry budget (poison-message quarantine). Terminal; stops blocking its entity's ordering.</summary>
    Failed,
}

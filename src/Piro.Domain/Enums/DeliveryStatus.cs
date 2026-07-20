namespace Piro.Domain.Enums;

/// <summary>
/// Outcome of a single delivery attempt recorded in <see cref="Entities.NotificationDeliveryLog"/> (RFC 0009
/// §4.6) — the ledger that provides both effectively-once idempotency and admin observability.
/// Persisted as a string.
/// </summary>
public enum DeliveryStatus
{
    /// <summary>The notification physically left to its destination.</summary>
    Delivered,

    /// <summary>The attempt failed (the error is retained on the row).</summary>
    Failed,

    /// <summary>Not sent because an identical delivery already succeeded (idempotency short-circuit).</summary>
    Skipped,
}

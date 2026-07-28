using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// One immutable record of a user-initiated change to an <see cref="Piro.Domain.Auditing.IAuditable"/>
/// entity, or of an authentication event (issue #17).
/// </summary>
/// <remarks>
/// Rows are append-only. Nothing updates or deletes them: there is no <c>UpdatedAt</c>, the API
/// exposes reads only, and the audit interceptor never revisits an entry it has written. An audit
/// trail that can be edited is not evidence of anything.
/// <para>
/// Entries are grouped by <see cref="CorrelationId"/> — one value per <c>SaveChanges</c>, so a
/// single user action that touches an entity plus its join rows (a Service and its tags) reads as
/// one transaction rather than four unrelated rows. <see cref="IsPrimary"/> marks which entry in the
/// group names the action. See <c>AuditLogConfiguration</c> for the indexes that back that grouping.
/// </para>
/// <para>
/// Retention is out of scope for the first cut — the table grows unbounded, same caveat as
/// <see cref="NotificationDeliveryLog"/>.
/// </para>
/// </remarks>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>
    /// Groups every entry written by one <c>SaveChanges</c>. A time-ordered UUIDv7, so ordering by
    /// this column is equivalent to ordering by transaction time and needs no aggregate — that is
    /// what makes group-paginated listing cheap.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// True for the one entry that names the transaction (the root entity a user acted on, in
    /// preference to its join rows). Exactly one per <see cref="CorrelationId"/>. Decided by the
    /// interceptor while the batch is still in memory, since inferring it later from stored rows is
    /// guesswork.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>The acting user's id. Never null: entries are only written for authenticated actors.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The acting user's email, denormalised on purpose. The audit trail must still read correctly
    /// after the account is renamed or deleted, which a join to <c>AspNetUsers</c> would not survive.
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    public AuditAction Action { get; set; }

    /// <summary>CLR name of the affected entity, e.g. "Service". Empty for authentication events.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Primary key of the affected entity, stringified because keys across the model are variously
    /// <c>int</c>, <c>Guid</c> and <c>string</c>. Composite keys are joined with '|'.
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable label for the affected row (a slug, name or title) so the admin feed does not
    /// have to resolve ids. Null when the entity exposes nothing suitable.
    /// </summary>
    public string? EntityLabel { get; set; }

    /// <summary>
    /// JSON snapshot of audited scalar properties before the change. Null on
    /// <see cref="AuditAction.Create"/> and on authentication events.
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// JSON snapshot of audited scalar properties after the change. Null on
    /// <see cref="AuditAction.Delete"/> and on authentication events.
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Caller IP. Behind the reverse proxy this is only meaningful with forwarded headers
    /// configured, otherwise it records the proxy. Null when unavailable.
    /// </summary>
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }
}

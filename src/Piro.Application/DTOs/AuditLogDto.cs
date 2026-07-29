using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>
/// One transaction in the audit feed: everything a single user action changed (issue #17).
/// </summary>
/// <remarks>
/// The feed paginates over transactions rather than rows, so a page size means a stable number of
/// user actions and no group is ever split across a page boundary. The actor fields live here rather
/// than on each entry because they are invariant within one <c>SaveChanges</c> — a batch has exactly
/// one actor by construction.
/// </remarks>
/// <param name="CorrelationId">Groups the entries; also the feed's sort key.</param>
/// <param name="OccurredAt">When the transaction was recorded.</param>
/// <param name="Action">The action of the entry that names this transaction.</param>
/// <param name="EntityType">The entity type that names this transaction.</param>
/// <param name="EntityLabel">Readable name of the affected row, when it has one.</param>
/// <param name="Entries">Every change in the transaction, including the primary one.</param>
public record AuditTransactionDto(
    Guid CorrelationId,
    DateTime OccurredAt,
    string UserId,
    string UserEmail,
    string? IpAddress,
    AuditAction Action,
    string EntityType,
    string? EntityLabel,
    IReadOnlyList<AuditEntryDto> Entries);

/// <summary>A single entity change within a transaction.</summary>
/// <param name="OldValues">JSON snapshot before the change; null for a create.</param>
/// <param name="NewValues">JSON snapshot after the change; null for a delete.</param>
public record AuditEntryDto(
    long Id,
    AuditAction Action,
    string EntityType,
    string EntityId,
    string? EntityLabel,
    string? OldValues,
    string? NewValues,
    DateTime CreatedAt);

/// <summary>Filters for the audit feed. All are optional and combine with AND.</summary>
/// <param name="EntityType">Restrict to one entity type, e.g. "Service".</param>
/// <param name="UserId">Restrict to one actor.</param>
/// <param name="Action">Restrict to one kind of action.</param>
/// <param name="From">Inclusive lower bound on the entry timestamp.</param>
/// <param name="To">Exclusive upper bound on the entry timestamp.</param>
public record AuditLogQueryParams(
    string? EntityType = null,
    string? UserId = null,
    AuditAction? Action = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 25);

/// <summary><paramref name="TotalCount"/> counts transactions, not entries.</summary>
public record AuditLogPageDto(
    IReadOnlyList<AuditTransactionDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

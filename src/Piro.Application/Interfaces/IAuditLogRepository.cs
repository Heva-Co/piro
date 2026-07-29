using Piro.Application.DTOs;

namespace Piro.Application.Interfaces;

/// <summary>
/// Read access to the audit trail (issue #17). Read-only by design: the table is append-only, and
/// the only writer is the audit interceptor, plus <see cref="IAuditLogWriter"/> for events that
/// never pass through <c>SaveChanges</c> on an audited entity.
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Returns one page of transactions, newest first. Paginates over transactions rather than rows,
    /// so a group is never split across pages and <c>TotalCount</c> counts user actions.
    /// </summary>
    Task<AuditLogPageDto> GetPagedAsync(AuditLogQueryParams query, CancellationToken ct = default);
}

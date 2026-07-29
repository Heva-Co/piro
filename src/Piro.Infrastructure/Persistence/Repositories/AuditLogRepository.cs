using Microsoft.EntityFrameworkCore;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class AuditLogRepository(PiroDbContext db) : IAuditLogRepository
{
    public async Task<AuditLogPageDto> GetPagedAsync(
        AuditLogQueryParams query,
        CancellationToken ct = default)
    {
        var filtered = ApplyFilters(db.AuditLogs.AsNoTracking(), query);

        var pageSize = Math.Clamp(query.PageSize, 10, 100);
        var page = Math.Max(1, query.Page);

        var total = await filtered
            .Select(l => l.CorrelationId)
            .Distinct()
            .CountAsync(ct);

        // Paginate over transactions, not rows: a page is a stable number of user actions, and no
        // group can be split across a page boundary.
        //
        // Ordered by the group's timestamp, with CorrelationId only as a tie-break. UUIDv7 is
        // time-ordered but only to millisecond precision — within one millisecond the remaining bits
        // are random — so sorting by the id alone would shuffle transactions written in the same
        // millisecond. That is not a rare edge case: one request can easily produce several.
        var correlationIds = await filtered
            .GroupBy(l => l.CorrelationId)
            .Select(g => new { CorrelationId = g.Key, OccurredAt = g.Max(l => l.CreatedAt) })
            .OrderByDescending(g => g.OccurredAt)
            .ThenByDescending(g => g.CorrelationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => g.CorrelationId)
            .ToListAsync(ct);

        if (correlationIds.Count == 0)
            return new AuditLogPageDto([], total, page, pageSize);

        // Deliberately unfiltered beyond the group ids: once a transaction is on the page, it is
        // shown whole. Re-applying the filters here would hide sibling entries and misrepresent what
        // the action actually changed.
        var rows = await db.AuditLogs
            .AsNoTracking()
            .Where(l => correlationIds.Contains(l.CorrelationId))
            .OrderBy(l => l.Id)
            .ToListAsync(ct);

        var byCorrelation = rows.GroupBy(l => l.CorrelationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Ordered by the paginated id list rather than by the dictionary, which has no order.
        var items = correlationIds
            .Where(byCorrelation.ContainsKey)
            .Select(id => ToTransaction(byCorrelation[id]))
            .ToList();

        return new AuditLogPageDto(items, total, page, pageSize);
    }

    private static IQueryable<AuditLog> ApplyFilters(IQueryable<AuditLog> q, AuditLogQueryParams query)
    {
        if (!string.IsNullOrWhiteSpace(query.EntityType))
            q = q.Where(l => l.EntityType == query.EntityType);

        if (!string.IsNullOrWhiteSpace(query.UserId))
            q = q.Where(l => l.UserId == query.UserId);

        if (query.Action.HasValue)
            q = q.Where(l => l.Action == query.Action.Value);

        if (query.From.HasValue)
            q = q.Where(l => l.CreatedAt >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(l => l.CreatedAt < query.To.Value);

        return q;
    }

    private static AuditTransactionDto ToTransaction(List<AuditLog> group)
    {
        // IsPrimary is set by the interceptor. The fallback covers rows written before that
        // guarantee existed, or any group whose primary was filtered out of the trail.
        var primary = group.FirstOrDefault(l => l.IsPrimary) ?? group[0];

        var entries = group
            .Select(l => new AuditEntryDto(
                l.Id,
                l.Action,
                l.EntityType,
                l.EntityId,
                l.EntityLabel,
                l.OldValues,
                l.NewValues,
                l.CreatedAt))
            .ToList();

        return new AuditTransactionDto(
            primary.CorrelationId,
            group.Max(l => l.CreatedAt),
            primary.UserId,
            primary.UserEmail,
            primary.IpAddress,
            primary.Action,
            primary.EntityType,
            primary.EntityLabel,
            entries);
    }
}

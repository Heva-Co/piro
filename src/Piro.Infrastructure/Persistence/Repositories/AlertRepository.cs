using Microsoft.EntityFrameworkCore;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAlertRepository"/>.
/// </summary>
internal class AlertRepository(PiroDbContext db) : IAlertRepository
{
    public async Task<Alert?> GetActiveForConfigAsync(int alertConfigId, CancellationToken ct = default)
    {
        return await db.Alerts.FirstOrDefaultAsync(a => a.AlertConfigId == alertConfigId && a.ResolvedAt == null, ct);
    }
        
    public async Task<Alert?> GetByExternalIdAsync(Piro.Domain.Enums.AlertSource source, string externalId, CancellationToken ct = default)
    {
        return  await db.Alerts.FirstOrDefaultAsync(a => a.Source == source && a.ExternalId == externalId, ct);
    }

    public async Task<Alert?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Alerts
            .Include(a => a.Check)
            .Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }
        

    public async Task<List<Alert>> GetActiveWithServiceEscalationAsync(CancellationToken ct = default)
    {
        return await db.Alerts
            .Include(a => a.Check)
            .Include(a => a.Service)
            .Include(a => a.AlertConfig)
            .Include(a => a.EscalationPolicy)
                .ThenInclude(p => p!.Steps)
                    .ThenInclude(s => s.Schedule)
            // EscalationPolicyId is a snapshot on Alert itself (RFC 0001 §4.6) — no longer needs to
            // navigate through Service, which also means this now naturally includes orphan alerts
            // (no Service, but an EscalationPolicyId copied from their Integration).
            .Where(a => a.ResolvedAt == null && a.EscalationPolicyId != null)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);
    }
        

    public async Task<Alert> CreateAsync(Alert alert, CancellationToken ct = default)
    {
        db.Alerts.Add(alert);
        await db.SaveChangesAsync(ct);
        return alert;
    }

    public async Task<Alert> UpdateAsync(Alert alert, CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
        return alert;
    }

    public async Task AddDeliveryLogAsync(EscalationDeliveryLog log, CancellationToken ct = default)
    {
        db.EscalationDeliveryLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<EscalationDeliveryLog>> GetDeliveryLogsAsync(int alertId, CancellationToken ct = default) =>
        await db.EscalationDeliveryLogs
            .Where(l => l.AlertId == alertId)
            .OrderByDescending(l => l.AttemptedAt)
            .ToListAsync(ct);

    public async Task<(IEnumerable<AlertSummaryRow> Items, int TotalCount, int AllTimeTotalCount)> GetPagedSummaryAsync(
        AlertQueryParams query, CancellationToken ct = default)
    {
        var allTimeTotal = await db.Alerts.CountAsync(ct);

        var q = db.Alerts.AsQueryable();

        if (query.From.HasValue)
            q = q.Where(a => a.FiredAt >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(a => a.FiredAt <= query.To.Value);

        if (query.ActiveOnly)
            q = q.Where(a => a.ResolvedAt == null);

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(query.PageSize, 10, 200);
        var page = Math.Max(1, query.Page);

        var items = await q
            // Active alerts (ResolvedAt == null) always sort before resolved ones, then most recent first.
            .OrderBy(a => a.ResolvedAt == null ? 0 : 1)
            .ThenByDescending(a => a.FiredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(AlertProjections.ToSummaryRow)
            .ToListAsync(ct);

        return (items, total, allTimeTotal);
    }

    public async Task<AlertDetailRow?> GetDetailByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Alerts
            .Where(a => a.Id == id)
            .Select(AlertProjections.ToDetailRow)
            .FirstOrDefaultAsync(ct);
    }
        
}

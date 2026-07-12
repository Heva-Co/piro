using Microsoft.EntityFrameworkCore;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAlertRepository"/>.</summary>
internal class AlertRepository(PiroDbContext db) : IAlertRepository
{
    public async Task<Alert?> GetActiveForConfigAsync(int alertConfigId, CancellationToken ct = default) =>
        await db.Alerts.FirstOrDefaultAsync(a => a.AlertConfigId == alertConfigId && a.ResolvedAt == null, ct);

    public async Task<Alert?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Alerts
            .Include(a => a.Check)
            .Include(a => a.Service)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<List<Alert>> GetActiveWithServiceEscalationAsync(CancellationToken ct = default) =>
        await db.Alerts
            .Include(a => a.Check)
            .Include(a => a.AlertConfig)
            .Include(a => a.Service)
                .ThenInclude(s => s.EscalationPolicy)
                    .ThenInclude(p => p!.Steps)
                        .ThenInclude(s => s.Schedule)
            .Where(a => a.ResolvedAt == null && a.Service.EscalationPolicyId != null)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

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
            .Select(a => new AlertSummaryRow(
                a.Id,
                a.Check.Slug,
                a.Check.Name,
                a.Service.Slug,
                a.Service.Name,
                a.AlertConfig.Description,
                a.Message,
                a.ImpactAtFireTime,
                a.FiredAt,
                a.ResolvedAt,
                a.OccurrenceCount,
                a.IncidentId,
                a.Service.EscalationPolicyId != null))
            .ToListAsync(ct);

        return (items, total, allTimeTotal);
    }

    public async Task<AlertDetailRow?> GetDetailByIdAsync(int id, CancellationToken ct = default) =>
        await db.Alerts
            .Where(a => a.Id == id)
            .Select(a => new AlertDetailRow(
                a.Id,
                a.Check.Slug,
                a.Check.Name,
                a.Service.Slug,
                a.Service.Name,
                a.AlertConfigId,
                a.AlertConfig.AlertFor,
                a.AlertConfig.AlertValue,
                a.AlertConfig.FailureThreshold,
                a.AlertConfig.SuccessThreshold,
                a.AlertConfig.Description,
                a.Message,
                a.ImpactAtFireTime,
                a.AlertConfig.Severity,
                a.FiredAt,
                a.ResolvedAt,
                a.OccurrenceCount,
                a.IncidentId,
                a.Incident != null ? a.Incident.Title : null,
                a.EscalationCurrentStep,
                a.AcknowledgedAt,
                a.AcknowledgedBy))
            .FirstOrDefaultAsync(ct);
}

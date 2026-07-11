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

    public async Task<int> CountConcurrentActiveAlertingServicesAsync(CancellationToken ct = default) =>
        await db.Alerts
            .Where(a => a.ResolvedAt == null && a.AlertConfig.CreateIncident)
            .Select(a => a.ServiceId)
            .Distinct()
            .CountAsync(ct);

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

    public async Task<(IEnumerable<AlertSummaryRow> Items, int TotalCount)> GetPagedSummaryAsync(
        AlertQueryParams query, CancellationToken ct = default)
    {
        var q = db.Alerts.AsQueryable();

        if (query.From.HasValue)
            q = q.Where(a => a.FiredAt >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(a => a.FiredAt <= query.To.Value);

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
                a.IncidentId))
            .ToListAsync(ct);

        return (items, total);
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
                a.Incident != null ? a.Incident.Title : null))
            .FirstOrDefaultAsync(ct);
}

using Microsoft.EntityFrameworkCore;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IMetricsRepository"/>.</summary>
internal class MetricsRepository(PiroDbContext db) : IMetricsRepository
{
    public async Task<DashboardMetricsDto> GetDashboardMetricsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var fromUnix = from.ToUnixTimeSeconds();
        var toUnix = to.ToUnixTimeSeconds();

        var incidentRows = await db.Incidents
            .Where(i => i.StartDateTime >= fromUnix && i.StartDateTime < toUnix)
            .Select(i => new
            {
                i.StartDateTime,
                i.EndDateTime,
                i.AcknowledgedAt,
                ServiceSlugs = i.IncidentServices.Select(s => new { s.Service.Slug, s.Service.Name }).ToList()
            })
            .ToListAsync(ct);

        var alertRows = await db.Alerts
            .Where(a => a.FiredAt >= from && a.FiredAt < to)
            .Select(a => new
            {
                a.FiredAt,
                a.IncidentId,
                IncidentCreatedAt = a.Incident != null ? a.Incident.CreatedAt : (DateTime?)null,
                Severity = a.AlertConfig.Severity
            })
            .ToListAsync(ct);

        double? mtta = Average(incidentRows
            .Where(r => r.AcknowledgedAt.HasValue)
            .Select(r => (double)(r.AcknowledgedAt!.Value - r.StartDateTime)));

        double? mttr = Average(incidentRows
            .Where(r => r.EndDateTime.HasValue)
            .Select(r => (double)(r.EndDateTime!.Value - r.StartDateTime)));

        // Only counts alerts that actually triggered their incident's creation (FiredAt <= CreatedAt) —
        // an alert hooked onto an already-existing per-service incident isn't a detection event, and
        // would otherwise produce a meaningless (often negative) delta.
        double? mttd = Average(alertRows
            .Where(r => r.IncidentCreatedAt.HasValue && r.FiredAt.UtcDateTime <= r.IncidentCreatedAt!.Value)
            .Select(r => (r.IncidentCreatedAt!.Value - r.FiredAt.UtcDateTime).TotalSeconds));

        double? noiseRatio = alertRows.Count == 0
            ? null
            : (double)alertRows.Count(r => r.IncidentId is not null) / alertRows.Count;

        var dailyCounts = incidentRows
            .GroupBy(r => DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(r.StartDateTime).UtcDateTime))
            .Select(g => new DailyIncidentCountDto(g.Key, g.Count()))
            .OrderBy(d => d.Date)
            .ToList();

        var byService = incidentRows
            .SelectMany(r => r.ServiceSlugs)
            .GroupBy(s => (s.Slug, s.Name))
            .Select(g => new ServiceIncidentCountDto(g.Key.Slug, g.Key.Name, g.Count()))
            .OrderByDescending(s => s.Count)
            .ToList();

        var bySeverity = alertRows
            .GroupBy(r => r.Severity.ToString())
            .Select(g => new SeverityIncidentCountDto(g.Key, g.Count()))
            .OrderByDescending(s => s.Count)
            .ToList();

        return new DashboardMetricsDto(
            DateOnly.FromDateTime(from.UtcDateTime),
            DateOnly.FromDateTime(to.UtcDateTime),
            mtta, mttr, mttd, noiseRatio,
            incidentRows.Count, alertRows.Count,
            dailyCounts, byService, bySeverity);
    }

    private static double? Average(IEnumerable<double> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : list.Average();
    }
}

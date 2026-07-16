using Microsoft.EntityFrameworkCore;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Enums;

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
                a.ResolvedAt,
                a.AcknowledgedAt,
                a.IncidentId,
                IncidentCreatedAt = a.Incident != null ? a.Incident.CreatedAt : (DateTime?)null,
                // Both null for a webhook-sourced/orphan alert (RFC 0001) — no internal AlertConfig
                // or Service behind it.
                Severity = a.AlertConfig != null ? a.AlertConfig.Severity : (AlertSeverity?)null,
                Slug = a.Service != null ? a.Service.Slug : null,
                Name = a.Service != null ? a.Service.Name : null
            })
            .ToListAsync(ct);

        var incidentMetrics = new IncidentMetricsDto(
            MttaSeconds: Average(incidentRows
                .Where(r => r.AcknowledgedAt.HasValue)
                .Select(r => (double)(r.AcknowledgedAt!.Value - r.StartDateTime))),
            MttrSeconds: Average(incidentRows
                .Where(r => r.EndDateTime.HasValue)
                .Select(r => (double)(r.EndDateTime!.Value - r.StartDateTime))),
            IncidentCount: incidentRows.Count
        );

        var alertMetrics = new AlertMetricsDto(
            MttaSeconds: Average(alertRows
                .Where(r => r.AcknowledgedAt.HasValue)
                .Select(r => (double)(DateTimeOffset.FromUnixTimeSeconds(r.AcknowledgedAt!.Value) - r.FiredAt).TotalSeconds)),
            MttrSeconds: Average(alertRows
                .Where(r => r.ResolvedAt.HasValue)
                .Select(r => (r.ResolvedAt!.Value - r.FiredAt).TotalSeconds)),
            // Only counts alerts actually linked within the range (FiredAt <= link's Incident.CreatedAt) —
            // this is "how long before a human declared/attached an incident", not automatic detection.
            MeanTimeToIncidentSeconds: Average(alertRows
                .Where(r => r.IncidentCreatedAt.HasValue && r.FiredAt.UtcDateTime <= r.IncidentCreatedAt!.Value)
                .Select(r => (r.IncidentCreatedAt!.Value - r.FiredAt.UtcDateTime).TotalSeconds)),
            AlertToIncidentConversionRate: alertRows.Count == 0
                ? null
                : (double)alertRows.Count(r => r.IncidentId is not null) / alertRows.Count,
            AlertCount: alertRows.Count,
            DailyAlertCounts: alertRows
                .GroupBy(r => DateOnly.FromDateTime(r.FiredAt.UtcDateTime))
                .Select(g => new DailyAlertCountDto(g.Key, g.Count()))
                .OrderBy(d => d.Date)
                .ToList(),
            // Both exclude external/orphan alerts (RFC 0001) — no Service/Severity to attribute them to.
            AlertsByService: alertRows
                .Where(r => r.Slug is not null && r.Name is not null)
                .GroupBy(r => (Slug: r.Slug!, Name: r.Name!))
                .Select(g => new ServiceAlertCountDto(g.Key.Slug, g.Key.Name, g.Count()))
                .OrderByDescending(s => s.Count)
                .ToList(),
            AlertsBySeverity: alertRows
                .Where(r => r.Severity is not null)
                .GroupBy(r => r.Severity!.Value.ToString())
                .Select(g => new SeverityIncidentCountDto(g.Key, g.Count()))
                .OrderByDescending(s => s.Count)
                .ToList()
        );

        var dailyIncidentCounts = incidentRows
            .GroupBy(r => DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(r.StartDateTime).UtcDateTime))
            .Select(g => new DailyIncidentCountDto(g.Key, g.Count()))
            .OrderBy(d => d.Date)
            .ToList();

        var incidentsByService = incidentRows
            .SelectMany(r => r.ServiceSlugs)
            .GroupBy(s => (s.Slug, s.Name))
            .Select(g => new ServiceIncidentCountDto(g.Key.Slug, g.Key.Name, g.Count()))
            .OrderByDescending(s => s.Count)
            .ToList();

        return new DashboardMetricsDto(
            DateOnly.FromDateTime(from.UtcDateTime),
            DateOnly.FromDateTime(to.UtcDateTime),
            incidentMetrics,
            alertMetrics,
            dailyIncidentCounts,
            incidentsByService);
    }

    private static double? Average(IEnumerable<double> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : list.Average();
    }
}

using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>Application service for the global Alerts overview list.</summary>
public class AlertAppService(IAlertRepository alertRepository)
{
    public async Task<AlertPageDto> GetPagedAsync(AlertQueryParams query, CancellationToken ct = default)
    {
        var (rows, total) = await alertRepository.GetPagedSummaryAsync(query, ct);
        var items = rows.Select(r => new AlertSummaryDto(
            r.Id, r.CheckSlug, r.CheckName, r.ServiceSlug, r.ServiceName,
            r.AlertConfigDescription, r.Message, r.ImpactAtFireTime,
            r.FiredAt, r.ResolvedAt, r.OccurrenceCount, r.IncidentId));
        return new AlertPageDto(items, total, Math.Max(1, query.Page), Math.Clamp(query.PageSize, 10, 200));
    }

    public async Task<AlertDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var row = await alertRepository.GetDetailByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Alert), id.ToString());
        return new AlertDetailDto(
            row.Id, row.CheckSlug, row.CheckName, row.ServiceSlug, row.ServiceName,
            row.AlertConfigId, row.AlertFor, row.AlertValue, row.FailureThreshold, row.SuccessThreshold,
            row.AlertConfigDescription, row.Message, row.ImpactAtFireTime, row.Severity,
            row.FiredAt, row.ResolvedAt, row.OccurrenceCount, row.IncidentId, row.IncidentTitle);
    }
}

using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>Application service for the global Alerts overview list, and manual Alert lifecycle actions
/// (linking to an Incident, acknowledging) — Piro no longer correlates Alerts into Incidents automatically.</summary>
public class AlertAppService(
    IAlertRepository alertRepository,
    IIncidentRepository incidentRepository,
    IncidentAppService incidentAppService)
{
    public async Task<AlertPageDto> GetPagedAsync(AlertQueryParams query, CancellationToken ct = default)
    {
        var (rows, total, allTimeTotal) = await alertRepository.GetPagedSummaryAsync(query, ct);
        var items = rows.Select(r => new AlertSummaryDto(
            r.Id, r.CheckSlug, r.CheckName, r.ServiceSlug, r.ServiceName,
            r.AlertConfigDescription, r.Message, r.ImpactAtFireTime,
            r.FiredAt, r.ResolvedAt, r.OccurrenceCount, r.IncidentId, r.HasEscalationPolicy));
        return new AlertPageDto(items, total, Math.Max(1, query.Page), Math.Clamp(query.PageSize, 10, 200), allTimeTotal);
    }

    public async Task<AlertDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var row = await alertRepository.GetDetailByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Alert), id.ToString());
        return new AlertDetailDto(
            row.Id, row.CheckSlug, row.CheckName, row.ServiceSlug, row.ServiceName,
            row.AlertConfigId, row.AlertFor, row.AlertValue, row.FailureThreshold, row.SuccessThreshold,
            row.AlertConfigDescription, row.Message, row.ImpactAtFireTime, row.Severity,
            row.FiredAt, row.ResolvedAt, row.OccurrenceCount, row.IncidentId, row.IncidentTitle,
            row.EscalationCurrentStep, row.AcknowledgedAt, row.AcknowledgedBy);
    }

    /// <summary>Returns the full on-call delivery history for this alert's escalation, most recent first.</summary>
    public async Task<IEnumerable<EscalationDeliveryLogDto>> GetEscalationLogsAsync(int alertId, CancellationToken ct = default)
    {
        var logs = await alertRepository.GetDeliveryLogsAsync(alertId, ct);
        return logs.Select(l => new EscalationDeliveryLogDto(
            l.StepIndex, l.UserName, l.ChannelType, l.Succeeded, l.ErrorMessage, l.AttemptedAt));
    }

    /// <summary>Returns all open incidents, for the "attach alert to incident" picker.</summary>
    public async Task<IEnumerable<IncidentDto>> GetOpenIncidentsAsync(CancellationToken ct = default)
    {
        var incidents = await incidentRepository.GetOpenAsync(ct);
        return incidents.Select(i => i.ToDto());
    }

    /// <summary>
    /// Links an Alert to an Incident: if <paramref name="incidentId"/> is null, creates a new
    /// ALERT-sourced incident for the Alert's service; otherwise attaches to the given existing
    /// incident. A human always decides this explicitly — Piro never links Alerts automatically.
    /// </summary>
    public async Task<AlertDetailDto> LinkToIncidentAsync(int alertId, LinkAlertToIncidentRequest request, CancellationToken ct = default)
    {
        var alert = await alertRepository.GetByIdAsync(alertId, ct)
            ?? throw new NotFoundException(nameof(Alert), alertId.ToString());

        if (alert.IncidentId is not null)
            throw new DomainValidationException("This alert is already linked to an incident.");

        Incident incident;
        if (request.IncidentId is int existingId)
        {
            incident = await incidentRepository.GetByIdAsync(existingId, ct)
                ?? throw new NotFoundException(nameof(Incident), existingId.ToString());

            if (!incident.IncidentServices.Any(s => s.ServiceId == alert.ServiceId))
            {
                incident.IncidentServices.Add(new IncidentService
                {
                    ServiceId = alert.ServiceId,
                    Impact = alert.ImpactAtFireTime,
                    TriggeringCheckId = alert.CheckId,
                });
                await incidentRepository.UpdateAsync(incident, ct);
            }
        }
        else
        {
            var created = await incidentAppService.CreateAlertIncidentAsync(
                IncidentTitleFactory.Build(alert.Check.Type), ct);
            created.IncidentServices.Add(new IncidentService
            {
                ServiceId = alert.ServiceId,
                Impact = alert.ImpactAtFireTime,
                TriggeringCheckId = alert.CheckId,
            });
            incident = await incidentRepository.CreateAsync(created, ct);
            await incidentRepository.AddTimelineEventAsync(new IncidentTimelineEvent
            {
                IncidentId = incident.Id,
                Type = TimelineEventType.Created,
                OccurredAt = DateTimeOffset.UtcNow,
                Visibility = EventVisibility.Private,
            }, ct);
        }

        alert.IncidentId = incident.Id;
        await alertRepository.UpdateAsync(alert, ct);
        await incidentRepository.AddTimelineEventAsync(new IncidentTimelineEvent
        {
            IncidentId = incident.Id,
            Type = TimelineEventType.AlertFired,
            OccurredAt = DateTimeOffset.UtcNow,
            AlertId = alert.Id,
            Visibility = EventVisibility.Private,
        }, ct);

        return await GetByIdAsync(alertId, ct);
    }

    /// <summary>Acknowledges an alert, pausing its on-call escalation (see EscalationCheckerService).</summary>
    public async Task<AlertDetailDto> AcknowledgeAsync(int alertId, string acknowledgedBy, CancellationToken ct = default)
    {
        var alert = await alertRepository.GetByIdAsync(alertId, ct)
            ?? throw new NotFoundException(nameof(Alert), alertId.ToString());

        if (alert.AcknowledgedAt is null)
        {
            alert.AcknowledgedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            alert.AcknowledgedBy = acknowledgedBy;
            alert.LastUserActivityAt = DateTimeOffset.UtcNow;
            await alertRepository.UpdateAsync(alert, ct);
        }

        return await GetByIdAsync(alertId, ct);
    }
}

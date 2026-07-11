using Piro.Application.DTOs;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Extensions;

public static class IncidentExtensions
{
    /// <summary>Maps an <see cref="Incident"/> to its admin-facing DTO. The timeline is fetched independently via GET /incidents/{id}/timeline.</summary>
    public static IncidentDto ToDto(this Incident i) => new(
        i.Id, i.Title, i.StartDateTime, i.EndDateTime,
        i.Status, i.IsResolved, i.Source,
        i.Visibility,
        i.IncidentServices.Select(s => new IncidentServiceDto(
            s.Service?.Slug ?? s.ServiceId.ToString(),
            s.Service?.Name ?? s.Service?.Slug ?? s.ServiceId.ToString(),
            s.Impact,
            s.TriggeringCheck?.Slug)),
        i.Alerts.Select(a => new AlertDto(
            a.Id, a.Check?.Slug ?? a.CheckId.ToString(), a.AlertConfig?.Description,
            a.Message, a.ImpactAtFireTime, a.FiredAt, a.ResolvedAt, a.OccurrenceCount)),
        MergedIntoIncidentId: i.MergesAsSource.FirstOrDefault()?.TargetIncidentId,
        i.CreatedAt, i.UpdatedAt,
        i.AcknowledgedAt, i.AcknowledgedBy,
        i.CurrentImpact,
        i.ImpactChanges.Select(c => new IncidentImpactChangeDto(c.Timestamp, c.Impact.ToString())),
        EscalationPolicyId: i.EscalationPolicyId,
        EscalationCurrentStep: i.EscalationCurrentStep,
        EscalationStepStartedAt: i.EscalationStepStartedAt,
        EscalationTotalSteps: null,
        NextEscalationAt: null
    );

    /// <summary>
    /// Maps an <see cref="Incident"/> to its public-facing DTO — omits internal-only fields
    /// (Source, AcknowledgedBy, escalation state) and non-hidden services only.
    /// The timeline is fetched independently via GET /incidents/{id}/timeline.
    /// </summary>
    public static PublicIncidentDto ToPublicDto(this Incident i) => new(
        i.Id, i.Title, i.StartDateTime, i.EndDateTime,
        i.Status, i.IsResolved,
        i.IncidentServices
            .Where(s => s.Service is null || !s.Service.IsHidden)
            .Select(s => new PublicIncidentServiceDto(
                s.Service?.Slug ?? s.ServiceId.ToString(),
                s.Service?.Name ?? s.Service?.Slug ?? s.ServiceId.ToString())),
        i.CurrentImpact,
        i.ImpactChanges.Select(c => new IncidentImpactChangeDto(c.Timestamp, c.Impact.ToString()))
    );
}

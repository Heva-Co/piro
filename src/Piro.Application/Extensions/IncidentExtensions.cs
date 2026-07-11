using Piro.Application.DTOs;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Extensions;

public static class IncidentExtensions
{
    /// <summary>
    /// Maps an <see cref="Incident"/> to its public-facing DTO — omits internal-only fields
    /// (Source, AcknowledgedBy, escalation state) and only includes Public timeline events / non-hidden services.
    /// </summary>
    public static PublicIncidentDto ToPublicDto(this Incident i) => new(
        i.Id, i.Title, i.StartDateTime, i.EndDateTime,
        i.Status, i.IsResolved, i.IsGlobal,
        i.TimelineEvents
            .Where(e => e.Visibility == EventVisibility.Public)
            .Select(e => new IncidentTimelineEventDto(
                e.Id, e.Type.ToString(), e.OccurredAt, e.ActorName, e.Comment,
                e.OldStatus, e.NewStatus, e.Visibility, e.RelatedIncidentId)),
        i.IncidentServices
            .Where(s => s.Service is null || !s.Service.IsHidden)
            .Select(s => new PublicIncidentServiceDto(
                s.Service?.Slug ?? s.ServiceId.ToString(),
                s.Service?.Name ?? s.Service?.Slug ?? s.ServiceId.ToString())),
        i.CurrentImpact,
        i.ImpactChanges.Select(c => new IncidentImpactChangeDto(c.Timestamp, c.Impact.ToString()))
    );
}

using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class PostmortemExtensions
{
    /// <summary>
    /// Maps a <see cref="Postmortem"/> to its full admin DTO, including analysis fields joined to their
    /// definitions and the timeline <em>derived</em> from the referenced incidents' events (RFC 0005 §4.4).
    /// Assumes the aggregate was loaded with field definitions and each incident's timeline/impact/alert data.
    /// </summary>
    public static PostmortemDto ToDto(this Postmortem p) => new(
        p.Id, p.Name, p.Status, p.ReviewOwnerUserId, p.ReviewOwnerName,
        p.ImpactStartAt, p.ImpactEndAt, p.PublishedAt, p.CreatedAt, p.UpdatedAt,
        p.FieldValues
            .Where(v => v.FieldDefinition is not null)
            .OrderBy(v => v.FieldDefinition.SortOrder)
            .Select(v => new PostmortemFieldValueDto(
                v.FieldDefinitionId, v.FieldDefinition.Key, v.FieldDefinition.Heading,
                v.FieldDefinition.HelpText, v.FieldDefinition.FieldType, v.FieldDefinition.SortOrder,
                v.FieldDefinition.IsSystem, v.Value)),
        p.PostmortemIncidents
            .Where(pi => pi.Incident is not null)
            .Select(pi => new PostmortemIncidentRefDto(
                pi.IncidentId, pi.Incident.Title, pi.Incident.Status,
                pi.Incident.StartDateTime, pi.Incident.EndDateTime, pi.Incident.CurrentImpact)),
        BuildDerivedTimeline(p)
    );

    /// <summary>Maps to the lightweight list-row DTO.</summary>
    public static PostmortemListItemDto ToListItemDto(this Postmortem p) => new(
        p.Id, p.Name, p.Status, p.ReviewOwnerName,
        p.ImpactStartAt, p.ImpactEndAt, p.PublishedAt,
        p.PostmortemIncidents.Count, p.CreatedAt, p.UpdatedAt
    );

    /// <summary>
    /// Merges every referenced incident's timeline events, impact changes, and firing alerts into one
    /// chronologically sorted, read-only list (RFC 0005 §4.4, Phase 1). Report-owned annotations are Phase 2.
    /// </summary>
    private static IEnumerable<PostmortemTimelineItemDto> BuildDerivedTimeline(Postmortem p)
    {
        var items = new List<PostmortemTimelineItemDto>();

        foreach (var link in p.PostmortemIncidents)
        {
            var incident = link.Incident;
            if (incident is null) continue;

            foreach (var e in incident.TimelineEvents)
                items.Add(new PostmortemTimelineItemDto(
                    incident.Id, incident.Title, $"incident:{e.Type}",
                    e.OccurredAt, e.ActorName, e.Comment, e.OldStatus, e.NewStatus, null));

            foreach (var c in incident.ImpactChanges)
                items.Add(new PostmortemTimelineItemDto(
                    incident.Id, incident.Title, "incident:ImpactChanged",
                    DateTimeOffset.FromUnixTimeSeconds(c.Timestamp), null, null, null, null, c.Impact));

            foreach (var a in incident.Alerts)
            {
                items.Add(new PostmortemTimelineItemDto(
                    incident.Id, incident.Title, "alert:Fired",
                    a.FiredAt, null, a.Message, null, null, a.ImpactAtFireTime));
                if (a.ResolvedAt.HasValue)
                    items.Add(new PostmortemTimelineItemDto(
                        incident.Id, incident.Title, "alert:Resolved",
                        a.ResolvedAt.Value, null, a.Message, null, null, null));
            }
        }

        return items.OrderBy(i => i.OccurredAt).ToList();
    }
}

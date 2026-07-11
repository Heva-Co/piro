using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>CRUD and lifecycle management for incidents.</summary>
public class IncidentAppService(
    IIncidentRepository incidentRepo,
    IServiceRepository serviceRepo,
    ServiceStatusService statusService,
    IEscalationPolicyRepository? escalationPolicyRepo = null)
{
    public async Task<IEnumerable<IncidentDto>> GetAllAsync(string filter = "active", CancellationToken ct = default)
    {
        var incidents = await incidentRepo.GetAllAsync(filter, ct);
        return incidents.Select(Map);
    }

    public async Task<IEnumerable<PublicIncidentDto>> GetAllPublicAsync(bool includeResolved = false, CancellationToken ct = default)
    {
        var incidents = await incidentRepo.GetAllPublicAsync(includeResolved, ct);
        return incidents.Select(i => i.ToPublicDto());
    }

    public async Task<IncidentDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var incident = await incidentRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString());
        return Map(incident);
    }

    /// <summary>Returns the incident only if it's publicly visible, with only public comments — for anonymous access.</summary>
    public async Task<PublicIncidentDto> GetPublicByIdAsync(int id, CancellationToken ct = default)
    {
        var incident = await incidentRepo.GetPublicByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString());
        return incident.ToPublicDto();
    }

    /// <summary>
    /// Creates an ALERT-sourced incident and assigns the global escalation policy if one exists.
    /// Always starts Private — publishing is always a manual action.
    /// Returns the raw entity so <see cref="AlertEvaluationService"/> can attach services before saving.
    /// </summary>
    public async Task<Incident> CreateAlertIncidentAsync(string title, bool isGlobal, CancellationToken ct = default)
    {
        var incident = new Incident
        {
            Title = title,
            StartDateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = IncidentStatus.Investigating,
            Source = "ALERT",
            IsGlobal = isGlobal,
            Visibility = IncidentVisibility.Private,
        };

        if (escalationPolicyRepo is not null)
        {
            var policy = await escalationPolicyRepo.GetSingleAsync(ct);
            if (policy is not null) incident.EscalationPolicyId = policy.Id;
        }

        return incident;
    }

    public async Task<IncidentDto> CreateAsync(CreateIncidentRequest request, CancellationToken ct = default)
    {
        var incident = new Incident
        {
            Title = request.Title,
            StartDateTime = request.StartDateTime,
            Status = request.Status,
            IsGlobal = request.IsGlobal,
            Source = "MANUAL",
            Visibility = IncidentVisibility.Private,
        };

        if (request.AffectedServices is not null)
        {
            foreach (var affected in request.AffectedServices)
            {
                var service = await serviceRepo.GetBySlugAsync(affected.ServiceSlug, ct)
                    ?? throw new NotFoundException(nameof(Service), affected.ServiceSlug);
                incident.IncidentServices.Add(new IncidentService
                {
                    ServiceId = service.Id,
                    Impact = affected.Impact
                });
            }
        }

        if (escalationPolicyRepo is not null)
        {
            var policy = await escalationPolicyRepo.GetSingleAsync(ct);
            if (policy is not null) incident.EscalationPolicyId = policy.Id;
        }

        var created = await incidentRepo.CreateAsync(incident, ct);
        await incidentRepo.AddTimelineEventAsync(new IncidentTimelineEvent
        {
            IncidentId = created.Id,
            Type = TimelineEventType.Created,
            OccurredAt = DateTimeOffset.UtcNow,
            Visibility = EventVisibility.Private,
        }, ct);
        await RecomputeAffectedAsync(created, ct);
        return Map(await incidentRepo.GetByIdAsync(created.Id, ct)
            ?? throw new NotFoundException(nameof(Incident), created.Id.ToString()));
    }

    public async Task<IncidentDto> UpdateAsync(int id, UpdateIncidentRequest request, CancellationToken ct = default)
    {
        var incident = await incidentRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString());

        if (request.Title is not null) incident.Title = request.Title;
        if (request.StartDateTime.HasValue) incident.StartDateTime = request.StartDateTime.Value;
        if (request.EndDateTime.HasValue) incident.EndDateTime = request.EndDateTime.Value;
        if (request.IsGlobal.HasValue) incident.IsGlobal = request.IsGlobal.Value;

        IncidentStatus? oldStatus = null;
        if (request.Status.HasValue && request.Status.Value != incident.Status)
        {
            oldStatus = incident.Status;
            incident.Status = request.Status.Value;
            if (request.Status.Value == IncidentStatus.Resolved)
                incident.EndDateTime ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        var updated = await incidentRepo.UpdateAsync(incident, ct);

        if (oldStatus.HasValue)
        {
            await incidentRepo.AddTimelineEventAsync(new IncidentTimelineEvent
            {
                IncidentId = updated.Id,
                Type = TimelineEventType.StatusChanged,
                OccurredAt = DateTimeOffset.UtcNow,
                OldStatus = oldStatus.Value,
                NewStatus = updated.Status,
                Visibility = EventVisibility.Private,
            }, ct);
        }

        await RecomputeAffectedAsync(updated, ct);

        return Map(await incidentRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString()));
    }

    /// <summary>
    /// Posts a comment on an incident. A comment and a status change are independent —
    /// <see cref="AddTimelineCommentRequest.Status"/> is optional; when present and different
    /// from the current status, a separate <see cref="TimelineEventType.StatusChanged"/> event
    /// is recorded before the <see cref="TimelineEventType.CommentPosted"/> one, sharing the
    /// same timestamp, so the status change reads first in the timeline.
    /// </summary>
    public async Task AddTimelineCommentAsync(int id, AddTimelineCommentRequest request, CancellationToken ct = default)
    {
        var incident = await incidentRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString());

        var isStatusChange = request.Status.HasValue && request.Status.Value != incident.Status;
        if (!isStatusChange && string.IsNullOrWhiteSpace(request.Comment))
            throw new DomainValidationException("An update must include either a comment or a status change.");

        var occurredAt = DateTimeOffset.UtcNow;

        if (isStatusChange)
        {
            var oldStatus = incident.Status;
            incident.Status = request.Status.Value;
            if (request.Status.Value == IncidentStatus.Resolved)
                incident.EndDateTime ??= occurredAt.ToUnixTimeSeconds();

            await incidentRepo.AddTimelineEventAsync(new IncidentTimelineEvent
            {
                IncidentId = incident.Id,
                Type = TimelineEventType.StatusChanged,
                OccurredAt = occurredAt,
                OldStatus = oldStatus,
                NewStatus = incident.Status,
                Visibility = EventVisibility.Private,
            }, ct);
        }

        if (!string.IsNullOrWhiteSpace(request.Comment))
        {
            await incidentRepo.AddTimelineEventAsync(new IncidentTimelineEvent
            {
                IncidentId = incident.Id,
                Type = TimelineEventType.CommentPosted,
                OccurredAt = occurredAt,
                Comment = request.Comment,
                // A comment can only be Public if its parent incident is also Public.
                Visibility = incident.IsPublic ? request.Visibility : EventVisibility.Private,
            }, ct);
        }

        incident.LastUserActivityAt = DateTimeOffset.UtcNow;
        await incidentRepo.UpdateAsync(incident, ct);
        await RecomputeAffectedAsync(incident, ct);
    }

    public async Task UpdateTimelineCommentAsync(int incidentId, int eventId, UpdateTimelineCommentRequest request, CancellationToken ct = default)
    {
        var rowsAffected = await incidentRepo.UpdateTimelineEventAsync(
            incidentId, eventId, request.Comment, request.Visibility, ct);
        if (rowsAffected == 0)
            throw new NotFoundException(nameof(IncidentTimelineEvent), eventId.ToString());
    }

    public async Task DeleteTimelineCommentAsync(int incidentId, int eventId, CancellationToken ct = default)
    {
        var evt = await incidentRepo.GetTimelineEventByIdAsync(incidentId, eventId, ct)
            ?? throw new NotFoundException(nameof(IncidentTimelineEvent), eventId.ToString());
        await incidentRepo.DeleteTimelineEventAsync(evt, ct);
    }

    public async Task<IncidentDto> SetServicesAsync(int incidentId, SetIncidentServicesRequest request, CancellationToken ct = default)
    {
        var incident = await incidentRepo.GetByIdAsync(incidentId, ct)
            ?? throw new NotFoundException(nameof(Incident), incidentId.ToString());

        var desired = request.Services.ToList();

        // Remove services no longer in the desired list
        var toRemove = incident.IncidentServices
            .Where(s => !desired.Any(d => d.ServiceSlug == (s.Service?.Slug ?? "")))
            .ToList();
        foreach (var link in toRemove)
        {
            await incidentRepo.RemoveServiceAsync(link, ct);
            await EmitAsync(incident, TimelineEventType.ServiceRemoved, ct);
        }

        // Add or update each desired service
        foreach (var d in desired)
        {
            var service = await serviceRepo.GetBySlugAsync(d.ServiceSlug, ct)
                ?? throw new NotFoundException(nameof(Service), d.ServiceSlug);

            var existing = incident.IncidentServices.FirstOrDefault(s => s.ServiceId == service.Id);
            if (existing is null)
            {
                var link = new IncidentService { IncidentId = incidentId, ServiceId = service.Id, Impact = d.Impact };
                await incidentRepo.AddServiceAsync(incident, link, ct);
                await EmitAsync(incident, TimelineEventType.ServiceAdded, ct);
            }
            else if (existing.Impact != d.Impact)
            {
                existing.Impact = d.Impact;
                await incidentRepo.UpdateServiceImpactAsync(existing, ct);
            }
        }

        await RecomputeAffectedAsync(incident, ct);
        return Map(await incidentRepo.GetByIdAsync(incidentId, ct)
            ?? throw new NotFoundException(nameof(Incident), incidentId.ToString()));
    }

    public async Task<IncidentDto> AddServiceAsync(int incidentId, AddIncidentServiceRequest request, CancellationToken ct = default)
    {
        var incident = await incidentRepo.GetByIdAsync(incidentId, ct)
            ?? throw new NotFoundException(nameof(Incident), incidentId.ToString());
        var service = await serviceRepo.GetBySlugAsync(request.ServiceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), request.ServiceSlug);

        // Avoid duplicates
        if (incident.IncidentServices.Any(s => s.ServiceId == service.Id))
            return Map(incident);

        var link = new IncidentService { IncidentId = incidentId, ServiceId = service.Id, Impact = request.Impact };
        await incidentRepo.AddServiceAsync(incident, link, ct);
        await EmitAsync(incident, TimelineEventType.ServiceAdded, ct);
        await statusService.ComputeAsync(service.Id, ct);
        return Map(await incidentRepo.GetByIdAsync(incidentId, ct)
            ?? throw new NotFoundException(nameof(Incident), incidentId.ToString()));
    }

    public async Task<IncidentDto> RemoveServiceAsync(int incidentId, string serviceSlug, CancellationToken ct = default)
    {
        var incident = await incidentRepo.GetByIdAsync(incidentId, ct)
            ?? throw new NotFoundException(nameof(Incident), incidentId.ToString());
        var service = await serviceRepo.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);
        var link = incident.IncidentServices.FirstOrDefault(s => s.ServiceId == service.Id)
            ?? throw new NotFoundException(nameof(IncidentService), serviceSlug);
        await incidentRepo.RemoveServiceAsync(link, ct);
        await EmitAsync(incident, TimelineEventType.ServiceRemoved, ct);
        await statusService.ComputeAsync(service.Id, ct);
        return Map(await incidentRepo.GetByIdAsync(incidentId, ct)
            ?? throw new NotFoundException(nameof(Incident), incidentId.ToString()));
    }

    public async Task<IncidentDto> AcknowledgeAsync(int id, string acknowledgedBy, CancellationToken ct = default)
    {
        var incident = await incidentRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString());

        if (incident.AcknowledgedAt.HasValue)
            return Map(incident);

        incident.AcknowledgedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        incident.AcknowledgedBy = acknowledgedBy;

        await incidentRepo.UpdateAsync(incident, ct);
        await incidentRepo.AddTimelineEventAsync(new IncidentTimelineEvent
        {
            IncidentId = incident.Id,
            Type = TimelineEventType.Acknowledged,
            OccurredAt = DateTimeOffset.UtcNow,
            ActorName = acknowledgedBy,
            Visibility = EventVisibility.Private,
        }, ct);
        return Map(await incidentRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString()));
    }

    public async Task PublishAsync(int id, CancellationToken ct = default)
    {
        var visibility = await incidentRepo.GetVisibilityAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString());
        if (visibility != IncidentVisibility.Public)
        {
            await incidentRepo.PublishAsync(id, ct);
            await incidentRepo.AddTimelineEventAsync(new IncidentTimelineEvent
            {
                IncidentId = id,
                Type = TimelineEventType.Published,
                OccurredAt = DateTimeOffset.UtcNow,
                Visibility = EventVisibility.Private,
            }, ct);

            // Only Public incidents count toward the publicly-shown service status —
            // publishing can newly surface impact that was previously masked.
            var incident = await incidentRepo.GetByIdAsync(id, ct)
                ?? throw new NotFoundException(nameof(Incident), id.ToString());
            await RecomputeAffectedAsync(incident, ct);
        }
    }

    /// <summary>Reverts an incident to Private. Also forces all its timeline events back to Private, since an event can never be Public while its parent incident isn't.</summary>
    public async Task UnpublishAsync(int id, CancellationToken ct = default)
    {
        var visibility = await incidentRepo.GetVisibilityAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString());
        if (visibility != IncidentVisibility.Public) return;

        await incidentRepo.MakeAllTimelineEventsPrivateAsync(id, ct);
        await incidentRepo.UnpublishAsync(id, ct);
        await incidentRepo.AddTimelineEventAsync(new IncidentTimelineEvent
        {
            IncidentId = id,
            Type = TimelineEventType.Unpublished,
            OccurredAt = DateTimeOffset.UtcNow,
            Visibility = EventVisibility.Private,
        }, ct);

        // Unpublishing hides its impact from the public status page — recompute so
        // affected services stop reporting DOWN/DEGRADED once the incident goes private.
        var incident = await incidentRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString());
        await RecomputeAffectedAsync(incident, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var incident = await incidentRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString());
        var affectedServiceIds = incident.IncidentServices.Select(s => s.ServiceId).ToList();
        var wasGlobal = incident.IsGlobal;
        await incidentRepo.DeleteAsync(incident, ct);
        if (wasGlobal)
        {
            var allServices = await serviceRepo.GetAllAsync(ct);
            foreach (var svc in allServices)
                await statusService.ComputeAsync(svc.Id, ct);
        }
        else
        {
            foreach (var sid in affectedServiceIds)
                await statusService.ComputeAsync(sid, ct);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Records an automatic, non-comment timeline event. Always Private — only CommentPosted can be made Public.</summary>
    private Task EmitAsync(Incident incident, TimelineEventType type, CancellationToken ct) =>
        incidentRepo.AddTimelineEventAsync(new IncidentTimelineEvent
        {
            IncidentId = incident.Id,
            Type = type,
            OccurredAt = DateTimeOffset.UtcNow,
            Visibility = EventVisibility.Private,
        }, ct);

    /// <summary>Triggers status recomputation for all services linked to the incident (or all services if global).</summary>
    private async Task RecomputeAffectedAsync(Incident incident, CancellationToken ct)
    {
        if (incident.IsGlobal)
        {
            var allServices = await serviceRepo.GetAllAsync(ct);
            foreach (var svc in allServices)
                await statusService.ComputeAsync(svc.Id, ct);
        }
        else
        {
            foreach (var link in incident.IncidentServices)
                await statusService.ComputeAsync(link.ServiceId, ct);
        }
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    private static IncidentDto Map(Incident i) => new(
        i.Id, i.Title, i.StartDateTime, i.EndDateTime,
        i.Status, i.IsResolved, i.IsGlobal, i.Source,
        i.Visibility,
        i.TimelineEvents.Select(e => new IncidentTimelineEventDto(
            e.Id, e.Type.ToString(), e.OccurredAt, e.ActorName, e.Comment,
            e.OldStatus, e.NewStatus, e.Visibility, e.RelatedIncidentId)),
        i.IncidentServices.Select(s => new IncidentServiceDto(
            s.Service?.Slug ?? s.ServiceId.ToString(),
            s.Service?.Name ?? s.Service?.Slug ?? s.ServiceId.ToString(),
            s.Impact,
            s.TriggeringCheck?.Slug)),
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
}

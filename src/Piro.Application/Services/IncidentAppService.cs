using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>CRUD and lifecycle management for incidents.</summary>
public class IncidentAppService(
    IIncidentRepository incidentRepo,
    IServiceRepository serviceRepo,
    ServiceStatusService statusService)
{
    public async Task<IEnumerable<IncidentDto>> GetAllAsync(bool includeResolved = false, CancellationToken ct = default)
    {
        var incidents = await incidentRepo.GetAllAsync(includeResolved, ct);
        return incidents.Select(Map);
    }

    public async Task<IncidentDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var incident = await incidentRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString());
        return Map(incident);
    }

    public async Task<IncidentDto> CreateAsync(CreateIncidentRequest request, CancellationToken ct = default)
    {
        var incident = new Incident
        {
            Title = request.Title,
            StartDateTime = request.StartDateTime,
            State = request.State,
            IsGlobal = request.IsGlobal,
            Status = IncidentStatus.Active,
            Source = "MANUAL"
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

        var created = await incidentRepo.CreateAsync(incident, ct);
        await RecomputeAffectedAsync(created, ct);
        return Map(created);
    }

    public async Task<IncidentDto> UpdateAsync(int id, UpdateIncidentRequest request, CancellationToken ct = default)
    {
        var incident = await incidentRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString());

        if (request.Title is not null) incident.Title = request.Title;
        if (request.StartDateTime.HasValue) incident.StartDateTime = request.StartDateTime.Value;
        if (request.EndDateTime.HasValue) incident.EndDateTime = request.EndDateTime.Value;
        if (request.State.HasValue)
        {
            incident.State = request.State.Value;
            // Advance to Resolved when state is Resolved
            if (request.State.Value == IncidentState.Resolved)
            {
                incident.Status = IncidentStatus.Resolved;
                incident.EndDateTime ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }

        var wasUnresolved = incident.Status != IncidentStatus.Resolved;
        var updated = await incidentRepo.UpdateAsync(incident, ct);
        await RecomputeAffectedAsync(updated, ct);

        return Map(updated);
    }

    public async Task AddCommentAsync(int id, AddCommentRequest request, CancellationToken ct = default)
    {
        var incident = await incidentRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Incident), id.ToString());

        var comment = new IncidentComment
        {
            IncidentId = incident.Id,
            Comment = request.Comment,
            CommentedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            State = request.State,
            Status = incident.Status
        };

        // Advance incident state when comment state progresses
        if (request.State == IncidentState.Resolved)
        {
            incident.Status = IncidentStatus.Resolved;
            incident.State = IncidentState.Resolved;
            incident.EndDateTime ??= comment.CommentedAt;
        }
        else
        {
            incident.State = request.State;
        }

        await incidentRepo.AddCommentAsync(incident, comment, ct);
        await RecomputeAffectedAsync(incident, ct);
    }

    public async Task UpdateCommentAsync(int incidentId, int commentId, UpdateCommentRequest request, CancellationToken ct = default)
    {
        var comment = await incidentRepo.GetCommentByIdAsync(incidentId, commentId, ct)
            ?? throw new NotFoundException(nameof(IncidentComment), commentId.ToString());
        comment.Comment = request.Comment;
        comment.State = request.State;
        await incidentRepo.UpdateCommentAsync(comment, ct);
    }

    public async Task DeleteCommentAsync(int incidentId, int commentId, CancellationToken ct = default)
    {
        var comment = await incidentRepo.GetCommentByIdAsync(incidentId, commentId, ct)
            ?? throw new NotFoundException(nameof(IncidentComment), commentId.ToString());
        await incidentRepo.DeleteCommentAsync(comment, ct);
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
        await statusService.ComputeAsync(service.Id, ct);
        return Map(await incidentRepo.GetByIdAsync(incidentId, ct)
            ?? throw new NotFoundException(nameof(Incident), incidentId.ToString()));
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
        i.Status, i.State, i.IsGlobal, i.Source,
        i.Comments.Select(c => new IncidentCommentDto(
            c.Id, c.Comment, c.CommentedAt, c.State, c.Status, c.CreatedAt)),
        i.IncidentServices.Select(s => new IncidentServiceDto(
            s.Service?.Slug ?? s.ServiceId.ToString(), s.Impact)),
        i.CreatedAt, i.UpdatedAt);
}

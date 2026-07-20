using Microsoft.AspNetCore.Identity;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>
/// CRUD and lifecycle management for postmortem reports (RFC 0005). A pure aggregate over new tables
/// that reads existing incident data for its derived timeline — no background job, dispatcher, or external I/O.
/// </summary>
public class PostmortemAppService(
    IPostmortemRepository postmortemRepo,
    UserManager<AppUser> userManager)
{
    public async Task<IEnumerable<PostmortemListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var postmortems = await postmortemRepo.GetAllAsync(ct);
        return postmortems.Select(p => p.ToListItemDto());
    }

    public async Task<PostmortemDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var postmortem = await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id);
        return postmortem.ToDto();
    }

    /// <summary>Returns the analysis template (active field definitions) — for rendering an empty editor.</summary>
    public async Task<IEnumerable<PostmortemFieldDefinitionDto>> GetFieldDefinitionsAsync(CancellationToken ct = default)
    {
        var defs = await postmortemRepo.GetActiveFieldDefinitionsAsync(ct);
        return defs.Select(d => new PostmortemFieldDefinitionDto(
            d.Id, d.Key, d.Heading, d.HelpText, d.FieldType, d.SortOrder, d.IsActive, d.IsSystem));
    }

    /// <summary>
    /// Creates a Draft report, snapshots the review owner's name, and seeds one empty field value per
    /// active definition (RFC 0005 §4.5).
    /// </summary>
    public async Task<PostmortemDto> CreateAsync(CreatePostmortemRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainValidationException("A postmortem must have a name.");

        var postmortem = new Postmortem
        {
            Name = request.Name.Trim(),
            Status = PostmortemStatus.Draft,
            ImpactStartAt = request.ImpactStartAt,
            ImpactEndAt = request.ImpactEndAt,
        };

        await ApplyReviewOwnerAsync(postmortem, request.ReviewOwnerUserId, ct);

        var definitions = await postmortemRepo.GetActiveFieldDefinitionsAsync(ct);
        foreach (var def in definitions)
            postmortem.FieldValues.Add(new PostmortemFieldValue { FieldDefinitionId = def.Id, Value = "" });

        var created = await postmortemRepo.CreateAsync(postmortem, ct);
        return (await postmortemRepo.GetByIdAsync(created.Id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), created.Id)).ToDto();
    }

    /// <summary>Updates report metadata and/or its analysis field values. Null fields are left unchanged.</summary>
    public async Task<PostmortemDto> UpdateAsync(int id, UpdatePostmortemRequest request, CancellationToken ct = default)
    {
        var postmortem = await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id);

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new DomainValidationException("A postmortem must have a name.");
            postmortem.Name = request.Name.Trim();
        }

        if (request.ImpactStartAt.HasValue) postmortem.ImpactStartAt = request.ImpactStartAt;
        if (request.ImpactEndAt.HasValue) postmortem.ImpactEndAt = request.ImpactEndAt;

        // ReviewOwnerUserId is intentionally always applied when present in the request so the owner can
        // be reassigned; the snapshot name is refreshed at the same time (RFC 0005 §4.7).
        if (request.ReviewOwnerUserId.HasValue)
            await ApplyReviewOwnerAsync(postmortem, request.ReviewOwnerUserId.Value, ct);

        if (request.Fields is not null)
        {
            foreach (var upd in request.Fields)
            {
                var value = postmortem.FieldValues.FirstOrDefault(v => v.FieldDefinitionId == upd.FieldDefinitionId);
                if (value is null)
                    throw new DomainValidationException(
                        $"Postmortem {id} has no field for definition {upd.FieldDefinitionId}.");
                value.Value = upd.Value ?? "";
            }
        }

        await postmortemRepo.UpdateAsync(postmortem, ct);
        return (await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id)).ToDto();
    }

    /// <summary>Marks a Draft report Published and stamps <see cref="Postmortem.PublishedAt"/> (mirrors incident publish).</summary>
    public async Task PublishAsync(int id, CancellationToken ct = default)
    {
        var postmortem = await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id);
        if (postmortem.Status == PostmortemStatus.Published) return;

        postmortem.Status = PostmortemStatus.Published;
        postmortem.PublishedAt = DateTimeOffset.UtcNow;
        await postmortemRepo.UpdateAsync(postmortem, ct);
    }

    /// <summary>Reverts a Published report to Draft and clears <see cref="Postmortem.PublishedAt"/>.</summary>
    public async Task UnpublishAsync(int id, CancellationToken ct = default)
    {
        var postmortem = await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id);
        if (postmortem.Status == PostmortemStatus.Draft) return;

        postmortem.Status = PostmortemStatus.Draft;
        postmortem.PublishedAt = null;
        await postmortemRepo.UpdateAsync(postmortem, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var postmortem = await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id);
        await postmortemRepo.DeleteAsync(postmortem, ct);
    }

    public async Task<PostmortemDto> LinkIncidentAsync(int id, LinkIncidentRequest request, CancellationToken ct = default)
    {
        _ = await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id);
        if (!await postmortemRepo.IncidentExistsAsync(request.IncidentId, ct))
            throw new NotFoundException(nameof(Incident), request.IncidentId);

        await postmortemRepo.LinkIncidentAsync(id, request.IncidentId, ct);
        return (await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id)).ToDto();
    }

    public async Task<PostmortemDto> UnlinkIncidentAsync(int id, int incidentId, CancellationToken ct = default)
    {
        _ = await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id);

        await postmortemRepo.UnlinkIncidentAsync(id, incidentId, ct);
        return (await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id)).ToDto();
    }

    public async Task<PostmortemDto> AddTimelineEntryAsync(int id, CreateTimelineEntryRequest request, string authorName, CancellationToken ct = default)
    {
        _ = await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id);
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new DomainValidationException("An annotation must have a body.");

        await postmortemRepo.AddTimelineEntryAsync(new PostmortemTimelineEntry
        {
            PostmortemId = id,
            OccurredAt = request.OccurredAt,
            Body = request.Body.Trim(),
            AuthorName = authorName,
        }, ct);

        return (await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id)).ToDto();
    }

    public async Task<PostmortemDto> UpdateTimelineEntryAsync(int id, int entryId, UpdateTimelineEntryRequest request, CancellationToken ct = default)
    {
        var entry = await postmortemRepo.GetTimelineEntryAsync(id, entryId, ct)
            ?? throw new NotFoundException(nameof(PostmortemTimelineEntry), entryId);
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new DomainValidationException("An annotation must have a body.");

        entry.OccurredAt = request.OccurredAt;
        entry.Body = request.Body.Trim();
        await postmortemRepo.UpdateTimelineEntryAsync(entry, ct);

        return (await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id)).ToDto();
    }

    public async Task<PostmortemDto> DeleteTimelineEntryAsync(int id, int entryId, CancellationToken ct = default)
    {
        var entry = await postmortemRepo.GetTimelineEntryAsync(id, entryId, ct)
            ?? throw new NotFoundException(nameof(PostmortemTimelineEntry), entryId);
        await postmortemRepo.DeleteTimelineEntryAsync(entry, ct);

        return (await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id)).ToDto();
    }

    /// <summary>
    /// Suggests incidents to link, from those overlapping the report's impact window. When the window is
    /// unset, falls back to the span of already-linked incidents; if that's empty too, returns nothing
    /// (nothing to anchor a suggestion on) — RFC 0005 §4.6, §8.
    /// </summary>
    public async Task<IEnumerable<PostmortemIncidentSuggestionDto>> GetIncidentSuggestionsAsync(int id, CancellationToken ct = default)
    {
        var postmortem = await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id);

        var (from, to) = ResolveSuggestionWindow(postmortem);
        if (from is null || to is null)
            return [];

        var incidents = await postmortemRepo.GetIncidentSuggestionsAsync(id, from.Value, to.Value, ct);
        return incidents.Select(i => new PostmortemIncidentSuggestionDto(
            i.Id, i.Title, i.Status, i.StartDateTime, i.EndDateTime));
    }

    /// <summary>
    /// Resolves the window used to suggest incidents: the explicit impact window if set, else the span of
    /// the linked incidents, else (null, null) meaning "no basis for a suggestion".
    /// </summary>
    private static (DateTimeOffset? From, DateTimeOffset? To) ResolveSuggestionWindow(Postmortem p)
    {
        if (p.ImpactStartAt.HasValue && p.ImpactEndAt.HasValue)
            return (p.ImpactStartAt, p.ImpactEndAt);

        var linked = p.PostmortemIncidents
            .Select(pi => pi.Incident)
            .Where(i => i is not null)
            .ToList();
        if (linked.Count == 0)
            return (null, null);

        var start = linked.Min(i => i!.StartDateTime);
        var end = linked.Max(i => i!.EndDateTime ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return (DateTimeOffset.FromUnixTimeSeconds(start), DateTimeOffset.FromUnixTimeSeconds(end));
    }

    /// <summary>
    /// Sets the review owner FK and snapshots the display name. A null id clears the owner (and its snapshot).
    /// Throws if a non-null id doesn't resolve to a user (RFC 0005 §4.2, §7).
    /// </summary>
    private async Task ApplyReviewOwnerAsync(Postmortem postmortem, int? reviewOwnerUserId, CancellationToken ct)
    {
        if (reviewOwnerUserId is null)
        {
            postmortem.ReviewOwnerUserId = null;
            postmortem.ReviewOwnerName = null;
            return;
        }

        var user = await userManager.FindByIdAsync(reviewOwnerUserId.Value.ToString())
            ?? throw new NotFoundException(nameof(AppUser), reviewOwnerUserId.Value);
        postmortem.ReviewOwnerUserId = user.Id;
        postmortem.ReviewOwnerName = string.IsNullOrWhiteSpace(user.Name) ? user.UserName : user.Name;
    }
}

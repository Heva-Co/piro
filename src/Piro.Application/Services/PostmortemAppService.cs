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
/// that reads existing incident data for its derived timeline. No background job, dispatcher, or external I/O.
/// </summary>
public class PostmortemAppService(
    IPostmortemRepository postmortemRepo,
    IPostmortemPdfGenerator pdfGenerator,
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

        // A definition may have been added after this report was created (Phase 3a). Backfill an empty
        // value row for any active definition the report is missing, then reload so it renders/edits.
        var activeIds = (await postmortemRepo.GetActiveFieldDefinitionsAsync(ct)).Select(d => d.Id);
        if (await postmortemRepo.BackfillFieldValuesAsync(id, activeIds, ct))
            postmortem = await postmortemRepo.GetByIdAsync(id, ct)
                ?? throw new NotFoundException(nameof(Postmortem), id);

        return postmortem.ToDto();
    }

    /// <summary>Returns the analysis template (active field definitions), for rendering an empty editor.</summary>
    public async Task<IEnumerable<PostmortemFieldDefinitionDto>> GetFieldDefinitionsAsync(CancellationToken ct = default)
    {
        var defs = await postmortemRepo.GetActiveFieldDefinitionsAsync(ct);
        return defs.Select(d => d.ToDto());
    }

    /// <summary>Returns every field definition (active + deactivated), for the template-management screen (Phase 3a).</summary>
    public async Task<IEnumerable<PostmortemFieldDefinitionDto>> GetAllFieldDefinitionsAsync(CancellationToken ct = default)
    {
        var defs = await postmortemRepo.GetAllFieldDefinitionsAsync(ct);
        return defs.Select(d => d.ToDto());
    }

    /// <summary>Creates a custom (non-system) analysis field, appended to the end of the template (Phase 3a).</summary>
    public async Task<PostmortemFieldDefinitionDto> CreateFieldDefinitionAsync(CreateFieldDefinitionRequest request, CancellationToken ct = default)
    {
        var key = (request.Key ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainValidationException("A field key is required.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[a-z0-9]+(?:_[a-z0-9]+)*$"))
            throw new DomainValidationException("Key must be lowercase letters, numbers, and underscores.");
        if (string.IsNullOrWhiteSpace(request.Heading))
            throw new DomainValidationException("A field heading is required.");
        if (await postmortemRepo.FieldDefinitionKeyExistsAsync(key, null, ct))
            throw new DomainValidationException($"A field with key '{key}' already exists.");

        var sortOrder = await postmortemRepo.GetMaxFieldDefinitionSortOrderAsync(ct) + 1;
        var created = await postmortemRepo.CreateFieldDefinitionAsync(new PostmortemFieldDefinition
        {
            Key = key,
            Heading = request.Heading.Trim(),
            HelpText = string.IsNullOrWhiteSpace(request.HelpText) ? null : request.HelpText.Trim(),
            FieldType = request.FieldType,
            SortOrder = sortOrder,
            IsActive = true,
            IsSystem = false,
        }, ct);
        return created.ToDto();
    }

    /// <summary>
    /// Edits a field definition. System fields (the eight seeded sections) allow only heading/help-text/active
    /// edits. Their key and type are immutable so existing reports keep resolving them (Phase 3a).
    /// </summary>
    public async Task<PostmortemFieldDefinitionDto> UpdateFieldDefinitionAsync(int id, UpdateFieldDefinitionRequest request, CancellationToken ct = default)
    {
        var def = await postmortemRepo.GetFieldDefinitionAsync(id, ct)
            ?? throw new NotFoundException(nameof(PostmortemFieldDefinition), id);

        if (request.Heading is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Heading))
                throw new DomainValidationException("A field heading is required.");
            def.Heading = request.Heading.Trim();
        }
        if (request.HelpText is not null)
            def.HelpText = string.IsNullOrWhiteSpace(request.HelpText) ? null : request.HelpText.Trim();
        if (request.SortOrder.HasValue)
            def.SortOrder = request.SortOrder.Value;
        if (request.IsActive.HasValue)
            def.IsActive = request.IsActive.Value;

        // The type is only mutable on custom fields. Changing a system field's type would reinterpret
        // the values existing reports already stored against it.
        if (request.FieldType.HasValue && request.FieldType.Value != def.FieldType)
        {
            if (def.IsSystem)
                throw new DomainValidationException("A system field's type cannot be changed.");
            def.FieldType = request.FieldType.Value;
        }

        await postmortemRepo.UpdateFieldDefinitionAsync(def, ct);
        return def.ToDto();
    }

    /// <summary>Reorders the template by assigning SortOrder from the given id sequence (Phase 3a).</summary>
    public async Task<IEnumerable<PostmortemFieldDefinitionDto>> ReorderFieldDefinitionsAsync(ReorderFieldDefinitionsRequest request, CancellationToken ct = default)
    {
        var all = await postmortemRepo.GetAllFieldDefinitionsAsync(ct);
        var byId = all.ToDictionary(d => d.Id);

        var order = 0;
        foreach (var defId in request.OrderedIds)
        {
            if (byId.TryGetValue(defId, out var def))
                def.SortOrder = order++;
        }
        await postmortemRepo.SaveFieldDefinitionOrderAsync(ct);
        return all.OrderBy(d => d.SortOrder).Select(d => d.ToDto());
    }

    /// <summary>
    /// Deletes a custom field. System fields can never be deleted; a custom field that reports have written
    /// values against is deactivated instead of hard-deleted, to preserve historical content (Phase 3a).
    /// </summary>
    public async Task DeleteFieldDefinitionAsync(int id, CancellationToken ct = default)
    {
        var def = await postmortemRepo.GetFieldDefinitionAsync(id, ct)
            ?? throw new NotFoundException(nameof(PostmortemFieldDefinition), id);
        if (def.IsSystem)
            throw new DomainValidationException("A system field cannot be deleted.");

        if (await postmortemRepo.FieldDefinitionHasValuesAsync(id, ct))
        {
            // In use, so soft-disable to keep historical values (the value FK is RESTRICT anyway).
            def.IsActive = false;
            await postmortemRepo.UpdateFieldDefinitionAsync(def, ct);
            return;
        }

        await postmortemRepo.DeleteFieldDefinitionAsync(def, ct);
    }

    /// <summary>
    /// Creates a Draft report, snapshots the review owner's name, and seeds one empty field value per
    /// active definition (RFC 0005 section 4.5).
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
        // be reassigned; the snapshot name is refreshed at the same time (RFC 0005 section 4.7).
        if (request.ReviewOwnerUserId.HasValue)
            await ApplyReviewOwnerAsync(postmortem, request.ReviewOwnerUserId.Value, ct);

        if (request.Fields is not null)
        {
            // Backfill any missing active-definition rows first (a definition may have been added after
            // this report was created, Phase 3a), then reload so the new rows are tracked and writable.
            var activeIds = (await postmortemRepo.GetActiveFieldDefinitionsAsync(ct)).Select(d => d.Id);
            if (await postmortemRepo.BackfillFieldValuesAsync(id, activeIds, ct))
                postmortem = await postmortemRepo.GetByIdAsync(id, ct)
                    ?? throw new NotFoundException(nameof(Postmortem), id);

            foreach (var upd in request.Fields)
            {
                var value = postmortem.FieldValues.FirstOrDefault(v => v.FieldDefinitionId == upd.FieldDefinitionId);
                // A value can be missing only if the definition is inactive/deleted, so skip silently
                // rather than fail the whole save on a stale field the client sent.
                if (value is null) continue;
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

    /// <summary>
    /// Renders the report to a downloadable PDF. Only a finalized (Published) report can be exported;
    /// a Draft is still being written, so exporting it would produce a misleading "final" document.
    /// </summary>
    public async Task<(byte[] Bytes, string FileName)> GeneratePdfAsync(int id, CancellationToken ct = default)
    {
        var postmortem = await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id);
        if (postmortem.Status != PostmortemStatus.Published)
            throw new DomainValidationException("Only a finalized postmortem can be exported to PDF. Finalize it first.");

        // GetByIdAsync backfills field-value rows for active definitions; do the same here so the PDF
        // includes every current section, not just the ones present when the report was created.
        var activeIds = (await postmortemRepo.GetActiveFieldDefinitionsAsync(ct)).Select(d => d.Id);
        if (await postmortemRepo.BackfillFieldValuesAsync(id, activeIds, ct))
            postmortem = await postmortemRepo.GetByIdAsync(id, ct)
                ?? throw new NotFoundException(nameof(Postmortem), id);

        var bytes = pdfGenerator.Generate(postmortem.ToDto(), DateTimeOffset.UtcNow);
        var slug = Slugify(postmortem.Name);
        return (bytes, $"postmortem-{slug}.pdf");
    }

    private static string Slugify(string name)
    {
        var lower = name.Trim().ToLowerInvariant();
        var chars = lower.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return string.IsNullOrEmpty(slug) ? "report" : slug;
    }

    public async Task<PostmortemDto> LinkIncidentAsync(int id, LinkIncidentRequest request, CancellationToken ct = default)
    {
        _ = await postmortemRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Postmortem), id);

        var status = await postmortemRepo.GetIncidentStatusAsync(request.IncidentId, ct)
            ?? throw new NotFoundException(nameof(Incident), request.IncidentId);

        // A postmortem is a post-incident review: only resolved/merged incidents can be referenced.
        // An in-progress incident has no final timeline to analyze yet.
        if (status is not (IncidentStatus.Resolved or IncidentStatus.Merged))
            throw new DomainValidationException(
                "Only resolved incidents can be linked to a postmortem. Resolve the incident first.");

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
    /// (nothing to anchor a suggestion on). See RFC 0005 section 4.6, section 8.
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
    /// Throws if a non-null id doesn't resolve to a user (RFC 0005 section 4.2, section 7).
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

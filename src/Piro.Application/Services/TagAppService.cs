using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>
/// Tag read/write and autocomplete for services, checks, and workers (RFC 0008, Part A). Enforces the §4.2
/// key/value rules and the per-entity ceiling, computes a check's effective tags via service inheritance
/// (§4.3), and merges the computed (on-read) system tags into a service read. Free-tag writes only ever
/// touch the User-source set; assignable <c>piro:*</c> tags (e.g. <c>piro:3rd-party</c>) are managed through
/// the dedicated assign/unassign methods, and reconciled <c>piro:*</c> tags are Piro-owned and read-only.
/// </summary>
public class TagAppService(ITagRepository tags, IEnumerable<IComputedSystemTagBatch<Service>> computedServiceTags)
{
    public async Task<EntityTagsDto> GetServiceTagsAsync(int serviceId, CancellationToken ct = default)
    {
        if (!await tags.ServiceExistsAsync(serviceId, ct))
            throw new NotFoundException(nameof(Service), serviceId);
        var own = (await tags.GetServiceTagsAsync(serviceId, ct))
            .Select(st => new TagDto(st.Tag.Key, st.Value))
            .ToList();
        own.AddRange(await ComputeServiceSystemTagsAsync(serviceId, ct));
        return new EntityTagsDto(own);
    }

    public async Task<CheckTagsDto> GetCheckTagsAsync(int checkId, CancellationToken ct = default)
    {
        var parentServiceId = await tags.GetParentServiceIdAsync(checkId, ct)
            ?? throw new NotFoundException(nameof(Check), checkId);

        var own = (await tags.GetCheckTagsAsync(checkId, ct))
            .Select(ct2 => new TagDto(ct2.Tag.Key, ct2.Value))
            .ToList();
        var serviceTags = (await tags.GetServiceTagsAsync(parentServiceId, ct))
            .Select(st => new TagDto(st.Tag.Key, st.Value))
            .ToList();
        // A check inherits its service's computed system tags too (e.g. piro:has-incident).
        serviceTags.AddRange(await ComputeServiceSystemTagsAsync(parentServiceId, ct));

        var effective = ComputeEffective(own, serviceTags);
        return new CheckTagsDto(own, effective);
    }

    public async Task<EntityTagsDto> GetWorkerTagsAsync(Guid workerId, CancellationToken ct = default)
    {
        if (!await tags.WorkerExistsAsync(workerId, ct))
            throw new NotFoundException(nameof(WorkerRegistration), workerId);
        var own = await tags.GetWorkerTagsAsync(workerId, ct);
        return new EntityTagsDto([.. own.Select(wt => new TagDto(wt.Tag.Key, wt.Value))]);
    }

    public async Task<EntityTagsDto> ReplaceServiceTagsAsync(int serviceId, ReplaceTagsRequest request, CancellationToken ct = default)
    {
        if (!await tags.ServiceExistsAsync(serviceId, ct))
            throw new NotFoundException(nameof(Service), serviceId);
        var resolved = await ValidateAndResolveAsync(request, ct);
        await tags.ReplaceServiceUserTagsAsync(serviceId, resolved, ct);
        return await GetServiceTagsAsync(serviceId, ct);
    }

    public async Task<CheckTagsDto> ReplaceCheckTagsAsync(int checkId, ReplaceTagsRequest request, CancellationToken ct = default)
    {
        if (!await tags.CheckExistsAsync(checkId, ct))
            throw new NotFoundException(nameof(Check), checkId);
        var resolved = await ValidateAndResolveAsync(request, ct);
        await tags.ReplaceCheckUserTagsAsync(checkId, resolved, ct);
        return await GetCheckTagsAsync(checkId, ct);
    }

    public async Task<EntityTagsDto> ReplaceWorkerTagsAsync(Guid workerId, ReplaceTagsRequest request, CancellationToken ct = default)
    {
        if (!await tags.WorkerExistsAsync(workerId, ct))
            throw new NotFoundException(nameof(WorkerRegistration), workerId);
        var resolved = await ValidateAndResolveAsync(request, ct);
        await tags.ReplaceWorkerUserTagsAsync(workerId, resolved, ct);
        return await GetWorkerTagsAsync(workerId, ct);
    }

    /// <summary>A check's required worker tags (RFC 0008 Part B, §4.5). Empty ⇒ the check runs on any worker.</summary>
    public async Task<EntityTagsDto> GetRequiredWorkerTagsAsync(int checkId, CancellationToken ct = default)
    {
        if (!await tags.CheckExistsAsync(checkId, ct))
            throw new NotFoundException(nameof(Check), checkId);
        var rows = await tags.GetRequiredWorkerTagsAsync(checkId, ct);
        return new EntityTagsDto([.. rows.Select(rt => new TagDto(rt.Tag.Key, rt.Value))]);
    }

    /// <summary>
    /// Replaces a check's required-worker-tag set. Each key references the shared worker-tag vocabulary, so
    /// both user keys and <c>piro:*</c> worker keys (e.g. <c>piro:region</c>) are allowed here, unlike the
    /// check's own user-tag set. Values and lengths are validated; the per-entity ceiling applies.
    /// </summary>
    public async Task<EntityTagsDto> ReplaceRequiredWorkerTagsAsync(int checkId, ReplaceTagsRequest request, CancellationToken ct = default)
    {
        if (!await tags.CheckExistsAsync(checkId, ct))
            throw new NotFoundException(nameof(Check), checkId);

        var byKey = new Dictionary<string, string?>();
        foreach (var tag in request.Tags)
        {
            var key = tag.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(key))
                throw new DomainValidationException("A required worker tag must name a key.");
            if (key.Length > TagConstants.MaxKeyLength)
                throw new DomainValidationException($"Tag key '{key}' exceeds the maximum length of {TagConstants.MaxKeyLength}.");

            var value = string.IsNullOrWhiteSpace(tag.Value) ? null : tag.Value.Trim();
            var valueError = TagValidation.ValidateValue(key, value);
            if (valueError is not null)
                throw new DomainValidationException(valueError);

            byKey[key] = value;
        }

        if (byKey.Count > TagConstants.MaxTagsPerEntity)
            throw new DomainValidationException($"A check may require at most {TagConstants.MaxTagsPerEntity} worker tags; {byKey.Count} were supplied.");

        var resolved = new List<(Tag, string?)>(byKey.Count);
        foreach (var (key, value) in byKey)
        {
            // A piro:* worker key is System-source; any other key is User-source. Reuse the catalog either way.
            var source = key.StartsWith(TagConstants.SystemNamespace, StringComparison.Ordinal) ? TagSource.System : TagSource.User;
            var tag = await tags.GetOrCreateTagAsync(key, source, ct);
            resolved.Add((tag, value));
        }

        await tags.ReplaceRequiredWorkerTagsAsync(checkId, resolved, ct);
        return await GetRequiredWorkerTagsAsync(checkId, ct);
    }

    /// <summary>
    /// Autocomplete keys. User keys always come from the DB. When <paramref name="includeSystem"/> is set,
    /// the curated <c>piro:*</c> system keys (<see cref="SystemTags.All"/>) are unioned in — these are a
    /// fixed vocabulary, not DB rows, so they're always offered even before any entity carries them. Used
    /// by the notification tag-selector, where filtering on e.g. <c>piro:3rd-party</c> is meaningful.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetKeysAsync(string? prefix, bool includeSystem = false, CancellationToken ct = default)
    {
        var userKeys = await tags.GetUserKeysAsync(prefix, ct);
        if (!includeSystem) return userKeys;

        var systemKeys = SystemTags.All
            .Select(d => d.Key)
            .Where(k => string.IsNullOrWhiteSpace(prefix) || k.StartsWith(prefix!, StringComparison.OrdinalIgnoreCase));

        return userKeys.Concat(systemKeys).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    public Task<IReadOnlyList<string>> GetValuesAsync(string key, CancellationToken ct = default) =>
        tags.GetValuesForKeyAsync(key, ct);

    /// <summary>
    /// Assigns (upserts) an assignable system tag on a service (§4.7). Rejects any key that is not an
    /// assignable entry in the <see cref="SystemTags"/> catalog, and validates the value against its
    /// <c>AllowedValues</c> vocabulary if it declares one.
    /// </summary>
    public async Task AssignServiceSystemTagAsync(int serviceId, string key, string? value, CancellationToken ct = default)
    {
        if (!await tags.ServiceExistsAsync(serviceId, ct))
            throw new NotFoundException(nameof(Service), serviceId);
        var resolvedValue = ValidateAssignableSystemTag(key, value);
        await tags.SetServiceSystemTagAsync(serviceId, key, resolvedValue, ct);
    }

    public async Task UnassignServiceSystemTagAsync(int serviceId, string key, CancellationToken ct = default)
    {
        if (!await tags.ServiceExistsAsync(serviceId, ct))
            throw new NotFoundException(nameof(Service), serviceId);
        EnsureAssignableKey(key);
        await tags.RemoveServiceSystemTagAsync(serviceId, key, ct);
    }

    public async Task AssignCheckSystemTagAsync(int checkId, string key, string? value, CancellationToken ct = default)
    {
        if (!await tags.CheckExistsAsync(checkId, ct))
            throw new NotFoundException(nameof(Check), checkId);
        var resolvedValue = ValidateAssignableSystemTag(key, value);
        await tags.SetCheckSystemTagAsync(checkId, key, resolvedValue, ct);
    }

    public async Task UnassignCheckSystemTagAsync(int checkId, string key, CancellationToken ct = default)
    {
        if (!await tags.CheckExistsAsync(checkId, ct))
            throw new NotFoundException(nameof(Check), checkId);
        EnsureAssignableKey(key);
        await tags.RemoveCheckSystemTagAsync(checkId, key, ct);
    }

    private static void EnsureAssignableKey(string key)
    {
        var def = SystemTags.Find(key);
        if (def is null || def.Assignment != SystemTagAssignment.Assignable)
            throw new DomainValidationException($"'{key}' is not an assignable system tag.");
    }

    /// <summary>Validates an assignable system tag and returns the value to store (null for a key-only flag).</summary>
    private static string? ValidateAssignableSystemTag(string key, string? value)
    {
        var def = SystemTags.Find(key);
        if (def is null || def.Assignment != SystemTagAssignment.Assignable)
            throw new DomainValidationException($"'{key}' is not an assignable system tag.");

        if (def.AllowedValues is null)
            return null; // key-only flag; presence is the flag, any supplied value is ignored

        if (string.IsNullOrWhiteSpace(value) || !def.AllowedValues.Contains(value))
            throw new DomainValidationException(
                $"'{key}' requires one of: {string.Join(", ", def.AllowedValues)}.");
        return value;
    }

    /// <summary>Computes the on-read system tags (§4.2) for a single service.</summary>
    private async Task<List<TagDto>> ComputeServiceSystemTagsAsync(int serviceId, CancellationToken ct)
    {
        var result = new List<TagDto>();
        foreach (var batch in computedServiceTags)
        {
            var matched = await batch.ComputeForAsync([serviceId], ct);
            if (matched.Contains(serviceId))
                result.Add(new TagDto(batch.Key, null));
        }
        return result;
    }

    /// <summary>
    /// Validates the requested user tags (§4.2), dedupes by key (last wins, since a key is unique per
    /// entity), enforces the per-entity ceiling, and resolves each key to its catalog <see cref="Tag"/>.
    /// </summary>
    private async Task<IReadOnlyList<(Tag Tag, string? Value)>> ValidateAndResolveAsync(ReplaceTagsRequest request, CancellationToken ct)
    {
        var byKey = new Dictionary<string, string?>();
        foreach (var tag in request.Tags)
        {
            var key = tag.Key?.Trim() ?? string.Empty;
            var keyError = TagValidation.ValidateUserKey(key);
            if (keyError is not null)
                throw new DomainValidationException(keyError);

            var value = string.IsNullOrWhiteSpace(tag.Value) ? null : tag.Value.Trim();
            var valueError = TagValidation.ValidateValue(key, value);
            if (valueError is not null)
                throw new DomainValidationException(valueError);

            byKey[key] = value; // a key is unique per entity; a repeated key replaces, not duplicates
        }

        if (byKey.Count > TagConstants.MaxTagsPerEntity)
            throw new DomainValidationException($"An entity may carry at most {TagConstants.MaxTagsPerEntity} tags; {byKey.Count} were supplied.");

        var resolved = new List<(Tag, string?)>(byKey.Count);
        foreach (var (key, value) in byKey)
        {
            var tag = await tags.GetOrCreateTagAsync(key, TagSource.User, ct);
            resolved.Add((tag, value));
        }
        return resolved;
    }

    /// <summary>
    /// A check's effective tags: its own tags unioned with the parent service's, own winning on key
    /// collision (§4.3).
    /// </summary>
    private static List<TagDto> ComputeEffective(IReadOnlyList<TagDto> own, IReadOnlyList<TagDto> serviceTags)
    {
        var ownKeys = own.Select(t => t.Key).ToHashSet();
        var effective = new List<TagDto>(own);
        foreach (var st in serviceTags)
            if (!ownKeys.Contains(st.Key))
                effective.Add(st);
        return effective;
    }
}

using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>
/// Tag read/write and autocomplete for services, checks, and workers (RFC 0008, Part A). Enforces the §4.2
/// key/value rules and the per-entity ceiling, and computes a check's effective tags via service
/// inheritance (§4.3). System tags are read-only here; they are managed through the separate system-tags
/// endpoints (Part A phase 2), so this service only ever replaces the User-source set.
/// </summary>
public class TagAppService(ITagRepository tags)
{
    public async Task<EntityTagsDto> GetServiceTagsAsync(int serviceId, CancellationToken ct = default)
    {
        if (!await tags.ServiceExistsAsync(serviceId, ct))
            throw new NotFoundException(nameof(Service), serviceId);
        var own = await tags.GetServiceTagsAsync(serviceId, ct);
        return new EntityTagsDto([.. own.Select(st => new TagDto(st.Tag.Key, st.Value))]);
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

    public Task<IReadOnlyList<string>> GetKeysAsync(string? prefix, CancellationToken ct = default) =>
        tags.GetUserKeysAsync(prefix, ct);

    public Task<IReadOnlyList<string>> GetValuesAsync(string key, CancellationToken ct = default) =>
        tags.GetValuesForKeyAsync(key, ct);

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

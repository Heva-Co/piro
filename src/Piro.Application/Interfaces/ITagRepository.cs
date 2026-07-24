using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>
/// Persistence contract for the tag catalog and the three join tables (RFC 0008, Part A). A single
/// repository owns the catalog plus service/check/worker assignments because they share the <see cref="Tag"/>
/// key catalog and the same find-or-create and replace-the-set operations.
/// </summary>
public interface ITagRepository
{
    /// <summary>Returns the catalog row for a key, or null if the key has never been used.</summary>
    Task<Tag?> GetTagByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>Returns the catalog row for a key, creating it with the given source if absent.</summary>
    Task<Tag> GetOrCreateTagAsync(string key, TagSource source, CancellationToken ct = default);

    Task<bool> ServiceExistsAsync(int serviceId, CancellationToken ct = default);
    Task<bool> CheckExistsAsync(int checkId, CancellationToken ct = default);
    Task<bool> WorkerExistsAsync(Guid workerId, CancellationToken ct = default);

    /// <summary>The service's own tag assignments (join rows with their catalog key), any source.</summary>
    Task<IReadOnlyList<ServiceTag>> GetServiceTagsAsync(int serviceId, CancellationToken ct = default);
    Task<IReadOnlyList<CheckTag>> GetCheckTagsAsync(int checkId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkerTag>> GetWorkerTagsAsync(Guid workerId, CancellationToken ct = default);

    /// <summary>A check's parent service id, for inheritance (§4.3). Null if the check does not exist.</summary>
    Task<int?> GetParentServiceIdAsync(int checkId, CancellationToken ct = default);

    /// <summary>Replaces the entity's <see cref="TagSource.User"/> tag rows with the given set; leaves system rows intact.</summary>
    Task ReplaceServiceUserTagsAsync(int serviceId, IReadOnlyList<(Tag Tag, string? Value)> tags, CancellationToken ct = default);
    Task ReplaceCheckUserTagsAsync(int checkId, IReadOnlyList<(Tag Tag, string? Value)> tags, CancellationToken ct = default);
    Task ReplaceWorkerUserTagsAsync(Guid workerId, IReadOnlyList<(Tag Tag, string? Value)> tags, CancellationToken ct = default);

    /// <summary>Distinct <see cref="TagSource.User"/> keys, optionally prefixed, for autocomplete (§4.7).</summary>
    Task<IReadOnlyList<string>> GetUserKeysAsync(string? prefix, CancellationToken ct = default);

    /// <summary>Distinct non-null values assigned for a key across all entities, for autocomplete (§4.7).</summary>
    Task<IReadOnlyList<string>> GetValuesForKeyAsync(string key, CancellationToken ct = default);

    // System (piro:*) tag assignment. The Tag catalog row is created with TagSource.System on first use.

    /// <summary>Upserts a stored system tag on a service (creates the catalog key if needed).</summary>
    Task SetServiceSystemTagAsync(int serviceId, string key, string? value, CancellationToken ct = default);
    Task SetCheckSystemTagAsync(int checkId, string key, string? value, CancellationToken ct = default);
    Task SetWorkerSystemTagAsync(Guid workerId, string key, string? value, CancellationToken ct = default);

    /// <summary>Removes a system tag assignment from a service (no-op if absent). The catalog key persists.</summary>
    Task RemoveServiceSystemTagAsync(int serviceId, string key, CancellationToken ct = default);
    Task RemoveCheckSystemTagAsync(int checkId, string key, CancellationToken ct = default);
    Task RemoveWorkerSystemTagAsync(Guid workerId, string key, CancellationToken ct = default);

    // Required worker tags (Part B scheduling): a check's constraint on which workers may run it.

    /// <summary>The check's required worker tags (join rows with their catalog key). Empty ⇒ run anywhere.</summary>
    Task<IReadOnlyList<CheckRequiredWorkerTag>> GetRequiredWorkerTagsAsync(int checkId, CancellationToken ct = default);

    /// <summary>Replaces a check's required-worker-tag set, resolving each key against the shared catalog.</summary>
    Task ReplaceRequiredWorkerTagsAsync(int checkId, IReadOnlyList<(Tag Tag, string? Value)> tags, CancellationToken ct = default);
}

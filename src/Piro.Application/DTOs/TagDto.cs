using System.ComponentModel.DataAnnotations;

namespace Piro.Application.DTOs;

/// <summary>One tag on an entity: a <see cref="Key"/> and an optional <see cref="Value"/> (RFC 0008).</summary>
public record TagDto(string Key, string? Value);

/// <summary>
/// The tags on a service or worker: the entity's own tags. (Checks additionally expose inherited/effective
/// tags via <see cref="CheckTagsDto"/>.)
/// </summary>
public record EntityTagsDto(IReadOnlyList<TagDto> Tags);

/// <summary>
/// A check's tags, split into its own tags and the effective set (own unioned with the parent service's,
/// own winning on key collision, §4.3).
/// </summary>
public record CheckTagsDto(IReadOnlyList<TagDto> Own, IReadOnlyList<TagDto> Effective);

/// <summary>
/// Replaces the full user-tag set on an entity (idempotent, §4.7). Only <see cref="Piro.Domain.Enums.TagSource.User"/>
/// tags are affected; <c>piro:*</c> keys are rejected and system tags are left untouched.
/// </summary>
public record ReplaceTagsRequest(
    [Required] IReadOnlyList<TagDto> Tags
);

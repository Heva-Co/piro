using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>
/// Full admin-facing representation of a postmortem report (RFC 0005). Includes the analysis fields
/// joined to their definitions, the referenced-incident summaries, and the timeline derived from those
/// incidents' events. Report-owned timeline annotations (§4.4) are a Phase 2 addition.
/// </summary>
public record PostmortemDto(
    int Id,
    string Name,
    PostmortemStatus Status,
    int? ReviewOwnerUserId,
    string? ReviewOwnerName,
    DateTimeOffset? ImpactStartAt,
    DateTimeOffset? ImpactEndAt,
    DateTimeOffset? PublishedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IEnumerable<PostmortemFieldValueDto> Fields,
    IEnumerable<PostmortemIncidentRefDto> Incidents,
    IEnumerable<PostmortemTimelineItemDto> Timeline
);

/// <summary>Lightweight list-row representation of a postmortem.</summary>
public record PostmortemListItemDto(
    int Id,
    string Name,
    PostmortemStatus Status,
    string? ReviewOwnerName,
    DateTimeOffset? ImpactStartAt,
    DateTimeOffset? ImpactEndAt,
    DateTimeOffset? PublishedAt,
    int IncidentCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>An analysis section's authored value joined to its definition (heading, help text, type, order).</summary>
public record PostmortemFieldValueDto(
    int FieldDefinitionId,
    string Key,
    string Heading,
    string? HelpText,
    PostmortemFieldType FieldType,
    int SortOrder,
    bool IsSystem,
    string Value
);

/// <summary>A definition in the analysis template (used to render the editor).</summary>
public record PostmortemFieldDefinitionDto(
    int Id,
    string Key,
    string Heading,
    string? HelpText,
    PostmortemFieldType FieldType,
    int SortOrder,
    bool IsActive,
    bool IsSystem
);

/// <summary>Summary of an incident referenced by a postmortem (a "data source").</summary>
public record PostmortemIncidentRefDto(
    int IncidentId,
    string Title,
    IncidentStatus Status,
    long StartDateTime,
    long? EndDateTime,
    ServiceStatus CurrentImpact
);

/// <summary>
/// One entry in the report's merged, chronologically sorted timeline. In Phase 1 every entry is
/// <em>derived</em> (read-only) from a referenced incident's events; <see cref="Source"/> distinguishes
/// which incident and event kind it came from.
/// </summary>
public record PostmortemTimelineItemDto(
    int IncidentId,
    string IncidentTitle,
    string Source,
    DateTimeOffset OccurredAt,
    string? ActorName,
    string? Text,
    IncidentStatus? OldStatus,
    IncidentStatus? NewStatus,
    ServiceStatus? Impact
);

/// <summary>Payload for creating a new postmortem report.</summary>
public record CreatePostmortemRequest(
    string Name,
    int? ReviewOwnerUserId,
    DateTimeOffset? ImpactStartAt,
    DateTimeOffset? ImpactEndAt
);

/// <summary>
/// Payload for updating report metadata and/or its analysis field values. Any field left null is
/// unchanged; <see cref="Fields"/>, when present, upserts the given values by field-definition id.
/// </summary>
public record UpdatePostmortemRequest(
    string? Name,
    int? ReviewOwnerUserId,
    DateTimeOffset? ImpactStartAt,
    DateTimeOffset? ImpactEndAt,
    IEnumerable<PostmortemFieldValueUpdate>? Fields
);

/// <summary>A single analysis field value to write, keyed by its definition id.</summary>
public record PostmortemFieldValueUpdate(int FieldDefinitionId, string Value);

/// <summary>Payload for linking an incident to a postmortem.</summary>
public record LinkIncidentRequest(int IncidentId);

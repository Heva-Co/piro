using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>Outbound representation of an incident.</summary>
public record IncidentDto(
    int Id,
    string Title,
    long StartDateTime,
    long? EndDateTime,
    [property: Obsolete("Use IsResolved (derived from State) instead.")]
    IncidentStatus Status,
    IncidentState State,
    bool IsResolved,
    bool IsGlobal,
    string? Source,
    bool IsPublic,
    IEnumerable<IncidentCommentDto> Comments,
    IEnumerable<IncidentServiceDto> Services,
    int? MergedIntoIncidentId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long? AcknowledgedAt,
    string? AcknowledgedBy,
    ServiceStatus CurrentImpact,
    IEnumerable<IncidentImpactChangeDto> ImpactChanges,
    int? EscalationPolicyId,
    int? EscalationCurrentStep,
    DateTimeOffset? EscalationStepStartedAt,
    int? EscalationTotalSteps,
    DateTimeOffset? NextEscalationAt
);

/// <summary>Point-in-time severity change recorded on an incident.</summary>
public record IncidentImpactChangeDto(long Timestamp, string Impact);

/// <summary>Outbound representation of a single incident status update.</summary>
public record IncidentCommentDto(
    int Id,
    string Comment,
    long CommentedAt,
    IncidentState State,
    [property: Obsolete("Use IncidentDto.IsResolved instead.")]
    IncidentStatus Status,
    DateTime CreatedAt
);

/// <summary>Service affected by an incident with its declared impact level.</summary>
public record IncidentServiceDto(string ServiceSlug, string? ServiceName, ServiceStatus Impact, string? TriggeringCheckSlug);

/// <summary>Payload for creating a new incident.</summary>
public record CreateIncidentRequest(
    string Title,
    long StartDateTime,
    IncidentState State,
    bool IsGlobal,
    IEnumerable<IncidentServiceImpact>? AffectedServices
);

/// <summary>Payload for updating incident metadata or advancing its state.</summary>
public record UpdateIncidentRequest(
    string? Title,
    long? StartDateTime,
    long? EndDateTime,
    IncidentState? State,
    bool? IsGlobal
);

/// <summary>Payload for posting a comment / state update on an incident.</summary>
public record AddCommentRequest(
    string Comment,
    IncidentState State
);

/// <summary>Payload for updating an existing comment.</summary>
public record UpdateCommentRequest(string Comment, IncidentState State);

/// <summary>Payload for adding a service to an existing incident.</summary>
public record AddIncidentServiceRequest(string ServiceSlug, ServiceStatus Impact);

/// <summary>Service + impact pair used in incident creation.</summary>
public record IncidentServiceImpact(string ServiceSlug, ServiceStatus Impact);

/// <summary>Full desired state of affected services — backend diffs and applies adds/removes/updates.</summary>
public record SetIncidentServicesRequest(IEnumerable<IncidentServiceImpact> Services);

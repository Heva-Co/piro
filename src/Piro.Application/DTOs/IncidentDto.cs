using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>Outbound representation of an incident (admin-facing — includes internal fields).</summary>
public record IncidentDto(
    int Id,
    string Title,
    long StartDateTime,
    long? EndDateTime,
    IncidentStatus Status,
    bool IsResolved,
    bool IsGlobal,
    string? Source,
    IncidentVisibility Visibility,
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

/// <summary>Outbound representation of a single incident status update (admin-facing).</summary>
public record IncidentCommentDto(
    int Id,
    string Comment,
    long CommentedAt,
    IncidentStatus Status,
    CommentVisibility Visibility,
    DateTime CreatedAt
);

/// <summary>
/// Outbound representation of an incident for the public status page.
/// Deliberately omits internal-only fields (Source, AcknowledgedBy, escalation state)
/// and only includes Public comments / non-hidden services.
/// </summary>
public record PublicIncidentDto(
    int Id,
    string Title,
    long StartDateTime,
    long? EndDateTime,
    IncidentStatus Status,
    bool IsResolved,
    bool IsGlobal,
    IEnumerable<PublicIncidentCommentDto> Comments,
    IEnumerable<PublicIncidentServiceDto> Services,
    ServiceStatus CurrentImpact,
    IEnumerable<IncidentImpactChangeDto> ImpactChanges
);

/// <summary>Public-facing status update.</summary>
public record PublicIncidentCommentDto(
    int Id,
    string Comment,
    long CommentedAt,
    IncidentStatus Status,
    DateTime CreatedAt
);

/// <summary>Service affected by an incident, public view — no triggering check exposed.</summary>
public record PublicIncidentServiceDto(string ServiceSlug, string? ServiceName);

/// <summary>Service affected by an incident with its declared impact level.</summary>
public record IncidentServiceDto(string ServiceSlug, string? ServiceName, ServiceStatus Impact, string? TriggeringCheckSlug);

/// <summary>Payload for creating a new incident.</summary>
public record CreateIncidentRequest(
    string Title,
    long StartDateTime,
    IncidentStatus Status,
    bool IsGlobal,
    IEnumerable<IncidentServiceImpact>? AffectedServices
);

/// <summary>Payload for updating incident metadata or advancing its status.</summary>
public record UpdateIncidentRequest(
    string? Title,
    long? StartDateTime,
    long? EndDateTime,
    IncidentStatus? Status,
    bool? IsGlobal
);

/// <summary>Payload for posting a comment / status update on an incident. Defaults to Private — must be explicitly made Public.</summary>
public record AddCommentRequest(
    string Comment,
    IncidentStatus Status,
    CommentVisibility Visibility = CommentVisibility.Private
);

/// <summary>Payload for updating an existing comment.</summary>
public record UpdateCommentRequest(string Comment, IncidentStatus Status, CommentVisibility Visibility);

/// <summary>Payload for adding a service to an existing incident.</summary>
public record AddIncidentServiceRequest(string ServiceSlug, ServiceStatus Impact);

/// <summary>Service + impact pair used in incident creation.</summary>
public record IncidentServiceImpact(string ServiceSlug, ServiceStatus Impact);

/// <summary>Full desired state of affected services — backend diffs and applies adds/removes/updates.</summary>
public record SetIncidentServicesRequest(IEnumerable<IncidentServiceImpact> Services);

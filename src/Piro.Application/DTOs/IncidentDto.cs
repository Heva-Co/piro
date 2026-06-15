using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>Outbound representation of an incident.</summary>
public record IncidentDto(
    int Id,
    string Title,
    long StartDateTime,
    long? EndDateTime,
    IncidentStatus Status,
    IncidentState State,
    bool IsGlobal,
    string? Source,
    IEnumerable<IncidentCommentDto> Comments,
    IEnumerable<IncidentServiceDto> Services,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>Outbound representation of a single incident status update.</summary>
public record IncidentCommentDto(
    int Id,
    string Comment,
    long CommentedAt,
    IncidentState State,
    IncidentStatus Status,
    DateTime CreatedAt
);

/// <summary>Service affected by an incident with its declared impact level.</summary>
public record IncidentServiceDto(string ServiceSlug, ServiceStatus Impact);

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
    IncidentState? State
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

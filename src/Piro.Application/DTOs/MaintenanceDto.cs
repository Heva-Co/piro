using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>Outbound representation of a maintenance definition.</summary>
public record MaintenanceDto(
    int Id,
    string Title,
    string? Description,
    long StartDateTime,
    string RRule,
    int DurationSeconds,
    MaintenanceStatus Status,
    bool IsGlobal,
    IEnumerable<MaintenanceEventDto> UpcomingEvents,
    IEnumerable<string> ServiceSlugs,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>Outbound representation of a single materialized maintenance occurrence.</summary>
public record MaintenanceEventDto(
    int Id,
    long StartDateTime,
    long EndDateTime,
    MaintenanceEventStatus Status
);

/// <summary>Payload for creating a maintenance window.</summary>
public record CreateMaintenanceRequest(
    string Title,
    string? Description,
    long StartDateTime,
    string RRule,
    int DurationSeconds,
    bool IsGlobal,
    IEnumerable<string>? ServiceSlugs
);

/// <summary>Payload for updating a maintenance window.</summary>
public record UpdateMaintenanceRequest(
    string? Title,
    string? Description,
    long? StartDateTime,
    string? RRule,
    int? DurationSeconds,
    bool? IsGlobal
);

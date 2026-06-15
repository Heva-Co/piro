using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

public record AlertConfigDto(
    int Id,
    int CheckId,
    AlertFor AlertFor,
    string AlertValue,
    int FailureThreshold,
    int SuccessThreshold,
    string? Description,
    bool CreateIncident,
    bool IsActive,
    bool IsAlerting,
    AlertSeverity Severity,
    List<int> TriggerIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateAlertConfigRequest(
    AlertFor AlertFor,
    string AlertValue,
    int FailureThreshold = 1,
    int SuccessThreshold = 1,
    string? Description = null,
    bool CreateIncident = false,
    bool IsActive = true,
    AlertSeverity Severity = AlertSeverity.Warning,
    List<int>? TriggerIds = null
);

public record UpdateAlertConfigRequest(
    AlertFor? AlertFor,
    string? AlertValue,
    int? FailureThreshold,
    int? SuccessThreshold,
    string? Description,
    bool? CreateIncident,
    bool? IsActive,
    AlertSeverity? Severity,
    List<int>? TriggerIds
);

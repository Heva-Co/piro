using Piro.Contracts;
using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

public record AlertConfigDto(
    int Id,
    int CheckId,
    string Dimension,
    DimensionComparison Comparison,
    ThresholdDirection Direction,
    string AlertValue,
    int FailureThreshold,
    int SuccessThreshold,
    string? Description,
    bool IsActive,
    bool IsAlerting,
    AlertSeverity Severity,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateAlertConfigRequest(
    string Dimension,
    string AlertValue,
    int FailureThreshold = 1,
    int SuccessThreshold = 1,
    string? Description = null,
    bool IsActive = true,
    AlertSeverity Severity = AlertSeverity.Warning
);

public record UpdateAlertConfigRequest(
    string? Dimension,
    string? AlertValue,
    int? FailureThreshold,
    int? SuccessThreshold,
    string? Description,
    bool? IsActive,
    AlertSeverity? Severity
);

using System.ComponentModel.DataAnnotations;
using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>Outbound representation of a check, returned by all check endpoints.</summary>
public record CheckDto(
    int Id,
    int ServiceId,
    string Slug,
    string Name,
    string? Description,
    CheckType Type,
    string Cron,
    string TypeDataJson,
    ServiceStatus CurrentStatus,
    bool IsActive,
    bool IsMultiRegion,
    int? FailureThreshold,
    int? RecoveryThreshold,
    int? HistoryDaysDesktop,
    int? HistoryDaysMobile,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? IntegrationId
);

/// <summary>Payload for creating a new check within a service.</summary>
public record CreateCheckRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Slug,
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    string? Description,
    CheckType Type,
    [Required] string Cron,
    [Required] string TypeDataJson,
    bool IsActive = true,
    bool IsMultiRegion = false,
    int? FailureThreshold = null,
    int? RecoveryThreshold = null,
    int? IntegrationId = null
);

/// <summary>Payload for updating an existing check. All fields are optional.</summary>
public record UpdateCheckRequest(
    string? Name,
    string? Description,
    string? Cron,
    string? TypeDataJson,
    bool? IsActive,
    bool? IsMultiRegion,
    int? FailureThreshold,
    int? RecoveryThreshold,
    int? HistoryDaysDesktop,
    int? HistoryDaysMobile,
    int? IntegrationId = null
);

/// <summary>Check with its parent service info — used in the global checks list.</summary>
public record CheckSummaryDto(
    int Id,
    string ServiceSlug,
    string ServiceName,
    string Slug,
    string Name,
    string? Description,
    CheckType Type,
    string Cron,
    ServiceStatus CurrentStatus,
    bool IsActive,
    bool IsMultiRegion,
    DateTime UpdatedAt,
    string? LastErrorMessage
);

/// <summary>A single execution log entry for a check.</summary>
public record CheckDataPointDto(
    long Timestamp,
    string Status,
    double? LatencyMs,
    string? DataType,
    string? ErrorMessage,
    string WorkerRegion
);

public record CheckDailyStatsDto(
    string Region,
    long Timestamp,
    int CountUp,
    int CountDown,
    int CountDegraded,
    double? AvgLatencyMs
);

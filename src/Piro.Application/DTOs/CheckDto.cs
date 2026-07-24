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
    int? HistoryDaysDesktop,
    int? HistoryDaysMobile,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? IntegrationId
);

/// <summary>
/// Payload for creating a new check within a service.
/// <c>AlertConfigs</c>, if provided, are created in the same transaction as the check
/// (e.g. from the admin's check-creation form). No alert configs are created automatically —
/// a check with none configured simply won't notify anyone until one is added.
/// </summary>
public record CreateCheckRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Slug,
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    string? Description,
    CheckType Type,
    [Required] string Cron,
    [Required] string TypeDataJson,
    bool IsActive = true,
    bool IsMultiRegion = false,
    Guid? IntegrationId = null,
    IReadOnlyList<CreateAlertConfigRequest>? AlertConfigs = null
);

/// <summary>Payload for updating an existing check. All fields are optional.</summary>
public record UpdateCheckRequest(
    string? Name,
    string? Description,
    string? Cron,
    string? TypeDataJson,
    bool? IsActive,
    bool? IsMultiRegion,
    [property: Obsolete]
    int? HistoryDaysDesktop,
    [property: Obsolete]
    int? HistoryDaysMobile,
    Guid? IntegrationId = null
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

/// <summary>
/// A single execution log entry for a check. <c>Dimensions</c> carries every numeric measurement the
/// check reported, keyed by dimension name (e.g. "Latency", "CertExpiry"); <c>LatencyMs</c> is a
/// convenience projection of the "Latency" entry. See <see cref="Piro.Domain.Entities.CheckDataPoint"/>.
/// </summary>
public record CheckDataPointDto(
    long Timestamp,
    string Status,
    double? LatencyMs,
    IReadOnlyDictionary<string, double> Dimensions,
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

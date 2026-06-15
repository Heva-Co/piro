using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>Public status summary for a service, safe to expose without authentication.</summary>
/// <remarks>
/// Never includes check details, propagation sources, or internal identifiers.
/// Status is the computed value from checks + dependency cascades.
/// </remarks>
public record PublicServiceDto(
    string Slug,
    string Name,
    string? Description,
    string? ImageUrl,
    ServiceStatus Status,
    int DisplayOrder,
    int HistoryDaysDesktop,
    int HistoryDaysMobile
);

/// <summary>A single minute-aligned status snapshot for the public history endpoint.</summary>
public record PublicStatusPointDto(
    long Timestamp,
    ServiceStatus Status
);

/// <summary>Uptime percentages for a service over a rolling window.</summary>
public record PublicUptimeDto(
    string Slug,
    int Days,
    double UptimePercent,
    long TotalMinutes,
    long UpMinutes
);

/// <summary>Per-day aggregated status and latency data for the service detail chart.</summary>
public record DailyStatsDto(
    long Timestamp,
    int CountUp,
    int CountDown,
    int CountDegraded,
    int CountMaintenance,
    double? AvgLatencyMs,
    double? MinLatencyMs,
    double? MaxLatencyMs
);

/// <summary>Complete data package for the public service detail page.</summary>
public record ServiceOverviewDto(
    string Slug,
    string Name,
    string? Description,
    string? ImageUrl,
    ServiceStatus CurrentStatus,
    long LastUpdatedAt,
    double? LastLatencyMs,
    double UptimePercent,
    double? OverallAvgLatencyMs,
    double? OverallMinLatencyMs,
    double? OverallMaxLatencyMs,
    long FromTimestamp,
    long ToTimestamp,
    IEnumerable<DailyStatsDto> DailyData
);

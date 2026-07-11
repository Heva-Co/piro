using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>Alert row for the global Alerts overview list — lightweight, no full incident/check payload.</summary>
public record AlertSummaryDto(
    int Id,
    string CheckSlug,
    string CheckName,
    string ServiceSlug,
    string ServiceName,
    string? AlertConfigDescription,
    string? Message,
    ServiceStatus ImpactAtFireTime,
    DateTimeOffset FiredAt,
    DateTimeOffset? ResolvedAt,
    int OccurrenceCount,
    int? IncidentId
);

/// <summary>
/// Query parameters for the paginated Alerts list. <see cref="From"/>/<see cref="To"/> filter on
/// <c>FiredAt</c>. Results always list active alerts (ResolvedAt == null) before resolved ones,
/// then most-recently-fired first within each group.
/// </summary>
public record AlertQueryParams(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50
);

/// <summary>A page of <see cref="AlertSummaryDto"/> results plus the total matching count.</summary>
public record AlertPageDto(
    IEnumerable<AlertSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

/// <summary>Full detail view of a single Alert, including the AlertConfig criteria that fired it.</summary>
public record AlertDetailDto(
    int Id,
    string CheckSlug,
    string CheckName,
    string ServiceSlug,
    string ServiceName,
    int AlertConfigId,
    AlertFor AlertFor,
    string AlertValue,
    int FailureThreshold,
    int SuccessThreshold,
    string? AlertConfigDescription,
    string? Message,
    ServiceStatus ImpactAtFireTime,
    AlertSeverity Severity,
    DateTimeOffset FiredAt,
    DateTimeOffset? ResolvedAt,
    int OccurrenceCount,
    int? IncidentId,
    string? IncidentTitle
);

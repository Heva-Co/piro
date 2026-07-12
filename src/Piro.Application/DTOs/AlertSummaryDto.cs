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
    int? IncidentId,
    bool HasEscalationPolicy
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
    int PageSize = 50,
    /// <summary>When true (default), only active (unresolved) alerts are returned.</summary>
    bool ActiveOnly = true
);

/// <summary>A page of <see cref="AlertSummaryDto"/> results plus the total matching count.</summary>
public record AlertPageDto(
    IEnumerable<AlertSummaryDto> Items,
    /// <summary>Count matching the current filters (From/To/ActiveOnly) — drives pagination.</summary>
    int TotalCount,
    int Page,
    int PageSize,
    /// <summary>Count of every Alert ever recorded, ignoring all filters — for "Total" stat display.</summary>
    int AllTimeTotalCount
) : PaginatedResponse<AlertSummaryDto>(Items, TotalCount, Page, PageSize);

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
    string? IncidentTitle,
    int? EscalationCurrentStep,
    long? AcknowledgedAt,
    string? AcknowledgedBy
);

/// <summary>Payload for linking an Alert to an Incident. Null IncidentId creates a new incident; otherwise attaches to the given one.</summary>
public record LinkAlertToIncidentRequest(int? IncidentId);

/// <summary>A single on-call delivery attempt in an alert's escalation history — see <see cref="Piro.Domain.Entities.EscalationDeliveryLog"/>.</summary>
public record EscalationDeliveryLogDto(
    int StepIndex,
    string UserName,
    IntegrationType ChannelType,
    bool Succeeded,
    string? ErrorMessage,
    DateTimeOffset AttemptedAt
);

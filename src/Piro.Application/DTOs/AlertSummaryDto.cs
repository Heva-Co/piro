using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>
/// Alert row for the global Alerts overview list — lightweight, no full incident/check payload.
/// Check/Service fields are null for an orphan alert (RFC 0001 §4.2) — no Check/Service to correlate against.
/// </summary>
public record AlertSummaryDto(
    int Id,
    string? CheckSlug,
    string? CheckName,
    string? ServiceSlug,
    string? ServiceName,
    string? AlertConfigDescription,
    string? Message,
    ServiceStatus ImpactAtFireTime,
    DateTimeOffset FiredAt,
    DateTimeOffset? ResolvedAt,
    int OccurrenceCount,
    int? IncidentId,
    bool HasEscalationPolicy,
    AlertSource Source,
    /// <summary>Display label for Source's origin Integration type (e.g. "GCP Cloud Monitoring") — null for Internal.</summary>
    string? SourceLabel,
    /// <summary>Iconify icon for Source's origin Integration type — null for Internal.</summary>
    string? SourceIconifyIcon
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

/// <summary>
/// Full detail view of a single Alert, including the AlertConfig criteria that fired it.
/// Check/Service fields are null for an orphan alert (RFC 0001 §4.2) — no Check/Service to correlate
/// against. AlertConfigId/AlertFor/AlertValue/thresholds/Severity are null for a webhook-sourced
/// alert (RFC 0001 §4.6) — there is no internal AlertConfig behind a third-party occurrence.
/// </summary>
public record AlertDetailDto(
    int Id,
    string? CheckSlug,
    string? CheckName,
    string? ServiceSlug,
    string? ServiceName,
    int? AlertConfigId,
    AlertFor? AlertFor,
    string? AlertValue,
    int? FailureThreshold,
    int? SuccessThreshold,
    string? AlertConfigDescription,
    string? Message,
    ServiceStatus ImpactAtFireTime,
    AlertSeverity? Severity,
    DateTimeOffset FiredAt,
    DateTimeOffset? ResolvedAt,
    int OccurrenceCount,
    int? IncidentId,
    string? IncidentTitle,
    int? EscalationCurrentStep,
    long? AcknowledgedAt,
    string? AcknowledgedBy,
    AlertSource Source,
    /// <summary>Display label for Source's origin Integration type (e.g. "GCP Cloud Monitoring") — null for Internal.</summary>
    string? SourceLabel,
    /// <summary>Iconify icon for Source's origin Integration type — null for Internal.</summary>
    string? SourceIconifyIcon,
    /// <summary>The exact webhook request body that created this alert (via SourceRequestLogId) — null for Internal or if the source log was since deleted.</summary>
    string? SourceRawPayload,
    /// <summary>Deep link into the source system's own console for this occurrence (e.g. GCP's incident URL) — null for Internal, or a source that doesn't provide one.</summary>
    string? SourceUrl
);

/// <summary>
/// Payload for linking an Alert to an Incident. Null IncidentId creates a new incident; otherwise
/// attaches to the given one. <see cref="ServiceIds"/> is used only when the Alert is orphan (no
/// ServiceId of its own — RFC 0001 §4.9); omitting it there creates/attaches to an Incident with
/// no IncidentService rows, which the rest of the system already tolerates.
/// </summary>
public record LinkAlertToIncidentRequest(int? IncidentId, IReadOnlyList<int>? ServiceIds = null);

/// <summary>
/// Request for the Data Retention "delete resolved alerts up to a date" action. Deletes only
/// resolved alerts (ResolvedAt strictly before <see cref="ResolvedBefore"/>) that are not linked to
/// an Incident — active and incident-linked alerts are always preserved.
/// </summary>
public record DeleteAlertsRequest(DateTimeOffset ResolvedBefore);

/// <summary>Result of a retention preview/delete — how many resolved alerts match the cutoff.</summary>
public record AlertRetentionResultDto(int Count);

/// <summary>A single on-call delivery attempt in an alert's escalation history — see <see cref="Piro.Domain.Entities.EscalationDeliveryLog"/>.</summary>
public record EscalationDeliveryLogDto(
    int StepIndex,
    string UserName,
    IntegrationType ChannelType,
    bool Succeeded,
    string? ErrorMessage,
    DateTimeOffset AttemptedAt
);

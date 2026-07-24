using Piro.Application.DTOs;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="Alert"/> entities.</summary>
public interface IAlertRepository
{
    /// <summary>Returns the currently active (ResolvedAt == null) Alert for the given AlertConfig, if any.</summary>
    Task<Alert?> GetActiveForConfigAsync(int alertConfigId, CancellationToken ct = default);

    /// <summary>
    /// Returns the Alert (active or resolved) matching this webhook source's own occurrence
    /// identifier — see <see cref="Alert.ExternalId"/> and RFC 0001 §4.8. Used for idempotent
    /// re-delivery/renotify handling, not just active-alert dedup: a GCP "closed" event for an
    /// already-resolved incident_id must still be recognized, not create a duplicate Alert.
    /// </summary>
    Task<Alert?> GetByExternalIdAsync(AlertSource source, string externalId, CancellationToken ct = default);

    /// <summary>Returns the full Alert entity (with Service/Check) by id, for mutation (link to incident, ack). Null if not found.</summary>
    Task<Alert?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Returns all active (ResolvedAt == null) Alerts with an EscalationPolicyId snapshot — the
    /// working set for <see cref="Services.EscalationCheckerService"/>. Includes Check, Service
    /// (both nullable — an orphan alert has neither, RFC 0001 §4.2), and the snapshotted
    /// EscalationPolicy with its Steps (and each Step's Schedule).
    /// </summary>
    Task<List<Alert>> GetActiveWithServiceEscalationAsync(CancellationToken ct = default);

    Task<Alert> CreateAsync(Alert alert, CancellationToken ct = default);
    Task<Alert> UpdateAsync(Alert alert, CancellationToken ct = default);

    /// <summary>Records a single on-call delivery attempt for an alert's escalation — see <see cref="EscalationDeliveryLog"/>.</summary>
    Task AddDeliveryLogAsync(EscalationDeliveryLog log, CancellationToken ct = default);

    /// <summary>Returns the full escalation delivery history for an alert, most recent first.</summary>
    Task<List<EscalationDeliveryLog>> GetDeliveryLogsAsync(int alertId, CancellationToken ct = default);

    /// <summary>
    /// Returns a paginated, lightweight summary of Alerts across the whole instance. Active alerts
    /// (ResolvedAt == null) are always ordered before resolved ones, then most-recently-fired first
    /// within each group. Alert is append-only, so this is the only supported way to browse history.
    /// </summary>
    Task<(IEnumerable<AlertSummaryRow> Items, int TotalCount, int AllTimeTotalCount)> GetPagedSummaryAsync(AlertQueryParams query, CancellationToken ct = default);

    /// <summary>Returns the full detail row for a single Alert, including its AlertConfig criteria and linked incident title, if any.</summary>
    Task<AlertDetailRow?> GetDetailByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Counts alerts eligible for retention cleanup: resolved (ResolvedAt != null) strictly before
    /// <paramref name="resolvedBefore"/> and not linked to an Incident (IncidentId == null). Preview
    /// for the destructive delete — same predicate as <see cref="DeleteResolvedBeforeAsync"/>.
    /// </summary>
    Task<int> CountResolvedBeforeAsync(DateTimeOffset resolvedBefore, CancellationToken ct = default);

    /// <summary>
    /// Permanently deletes resolved alerts (ResolvedAt != null) resolved strictly before
    /// <paramref name="resolvedBefore"/> that are not linked to an Incident (IncidentId == null) —
    /// their EscalationDeliveryLogs cascade. Active alerts and incident-linked alerts are never
    /// touched. Returns the number of alerts deleted.
    /// </summary>
    Task<int> DeleteResolvedBeforeAsync(DateTimeOffset resolvedBefore, CancellationToken ct = default);
}

/// <summary>Flat projection of an Alert plus the AlertConfig criteria and linked incident title — no full entity load.</summary>
public record AlertDetailRow(
    int Id,
    string? CheckSlug,
    string? CheckName,
    string? ServiceSlug,
    string? ServiceName,
    int? AlertConfigId,
    string? Dimension,
    string? AlertValue,
    int? FailureThreshold,
    int? SuccessThreshold,
    string? AlertConfigDescription,
    string? Message,
    Piro.Domain.Enums.ServiceStatus ImpactAtFireTime,
    Piro.Domain.Enums.AlertSeverity? Severity,
    DateTimeOffset FiredAt,
    DateTimeOffset? ResolvedAt,
    int OccurrenceCount,
    int? IncidentId,
    string? IncidentTitle,
    int? EscalationCurrentStep,
    /// <summary>Set when escalation stopped after the last step exhausted its retries (RFC 0006) — terminal, distinct from ResolvedAt.</summary>
    DateTimeOffset? EscalationExhaustedAt,
    long? AcknowledgedAt,
    string? AcknowledgedBy,
    Piro.Domain.Enums.AlertSource Source,
    /// <summary>The exact webhook request body that created this alert, via SourceRequestLogId — null for Internal.</summary>
    string? SourceRawPayload,
    /// <summary>Deep link into the source system's own console for this occurrence — null for Internal.</summary>
    string? SourceUrl
);

/// <summary>Flat projection of an Alert plus the display fields needed for a list view — no full entity load.</summary>
public record AlertSummaryRow(
    int Id,
    string? CheckSlug,
    string? CheckName,
    string? ServiceSlug,
    string? ServiceName,
    string? AlertConfigDescription,
    string? Message,
    Piro.Domain.Enums.ServiceStatus ImpactAtFireTime,
    DateTimeOffset FiredAt,
    DateTimeOffset? ResolvedAt,
    int OccurrenceCount,
    int? IncidentId,
    bool HasEscalationPolicy,
    Piro.Domain.Enums.AlertSource Source
);

using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="Alert"/> entities.</summary>
public interface IAlertRepository
{
    /// <summary>Returns the currently active (ResolvedAt == null) Alert for the given AlertConfig, if any.</summary>
    Task<Alert?> GetActiveForConfigAsync(int alertConfigId, CancellationToken ct = default);

    /// <summary>Returns the full Alert entity (with Service/Check) by id, for mutation (link to incident, ack). Null if not found.</summary>
    Task<Alert?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Returns all active (ResolvedAt == null) Alerts whose Service has an EscalationPolicy assigned —
    /// the working set for <see cref="Services.EscalationCheckerService"/>. Includes Check, Service,
    /// and the Service's EscalationPolicy with its Steps (and each Step's Schedule).
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
}

/// <summary>Flat projection of an Alert plus the AlertConfig criteria and linked incident title — no full entity load.</summary>
public record AlertDetailRow(
    int Id,
    string CheckSlug,
    string CheckName,
    string ServiceSlug,
    string ServiceName,
    int AlertConfigId,
    Piro.Domain.Enums.AlertFor AlertFor,
    string AlertValue,
    int FailureThreshold,
    int SuccessThreshold,
    string? AlertConfigDescription,
    string? Message,
    Piro.Domain.Enums.ServiceStatus ImpactAtFireTime,
    Piro.Domain.Enums.AlertSeverity Severity,
    DateTimeOffset FiredAt,
    DateTimeOffset? ResolvedAt,
    int OccurrenceCount,
    int? IncidentId,
    string? IncidentTitle,
    int? EscalationCurrentStep,
    long? AcknowledgedAt,
    string? AcknowledgedBy
);

/// <summary>Flat projection of an Alert plus the display fields needed for a list view — no full entity load.</summary>
public record AlertSummaryRow(
    int Id,
    string CheckSlug,
    string CheckName,
    string ServiceSlug,
    string ServiceName,
    string? AlertConfigDescription,
    string? Message,
    Piro.Domain.Enums.ServiceStatus ImpactAtFireTime,
    DateTimeOffset FiredAt,
    DateTimeOffset? ResolvedAt,
    int OccurrenceCount,
    int? IncidentId,
    bool HasEscalationPolicy
);

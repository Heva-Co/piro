namespace Piro.Application.DTOs;

/// <summary>
/// Incident/alert response-time and volume metrics for a given date range. Alerts are their own
/// first-class entity that may never reach an Incident (linking one is a manual, optional action),
/// so MTTA/MTTR are tracked separately for each: <see cref="IncidentMetrics"/> reflects only the
/// subset of activity that was ever escalated to a declared incident, while <see cref="AlertMetrics"/>
/// reflects every alert regardless of whether a human ever created/linked an incident for it.
/// </summary>
public record DashboardMetricsDto(
    DateOnly From,
    DateOnly To,

    IncidentMetricsDto IncidentMetrics,
    AlertMetricsDto AlertMetrics,

    IEnumerable<DailyIncidentCountDto> DailyIncidentCounts,
    IEnumerable<ServiceIncidentCountDto> IncidentsByService
);

/// <summary>Response-time metrics computed only over Incidents (manually declared/linked).</summary>
public record IncidentMetricsDto(
    /// <summary>Mean seconds from Incident.StartDateTime to Incident.AcknowledgedAt, across incidents
    /// acknowledged within the range. Null if none in range have been acknowledged yet.</summary>
    double? MttaSeconds,

    /// <summary>Mean seconds from Incident.StartDateTime to Incident.EndDateTime, across incidents
    /// resolved within the range. Null if none in range have been resolved yet.</summary>
    double? MttrSeconds,

    int IncidentCount
);

/// <summary>
/// Response-time and volume metrics computed over every Alert, independent of whether it was ever
/// linked to an Incident — this is the metric set that reflects on-call responsiveness in the
/// common case where an alert is acknowledged/resolved without ever becoming a declared incident.
/// </summary>
public record AlertMetricsDto(
    /// <summary>Mean seconds from Alert.FiredAt to Alert.AcknowledgedAt, across alerts acknowledged
    /// within the range. Null if none in range have been acknowledged yet.</summary>
    double? MttaSeconds,

    /// <summary>Mean seconds from Alert.FiredAt to Alert.ResolvedAt, across alerts resolved within
    /// the range. Null if none in range have been resolved yet.</summary>
    double? MttrSeconds,

    /// <summary>
    /// Mean seconds from Alert.FiredAt to the CreatedAt of the Incident a human linked it to —
    /// only over alerts that were actually linked within the range. Distinct from MTTD: this
    /// measures how long an alert sat before a human decided to declare/attach an incident for it,
    /// not automatic detection latency (there is none — linking is always a manual action now).
    /// Null if no alert in range has been linked to an incident yet.
    /// </summary>
    double? MeanTimeToIncidentSeconds,

    /// <summary>count(Alert linked to an Incident) / count(Alert) within the range — what fraction
    /// of alerts were ever considered incident-worthy by a human. Null if no alerts fired in range.</summary>
    double? AlertToIncidentConversionRate,

    int AlertCount,

    IEnumerable<DailyAlertCountDto> DailyAlertCounts,
    IEnumerable<ServiceAlertCountDto> AlertsByService,
    IEnumerable<SeverityIncidentCountDto> AlertsBySeverity
);

public record DailyIncidentCountDto(DateOnly Date, int Count);

public record DailyAlertCountDto(DateOnly Date, int Count);

public record ServiceIncidentCountDto(string ServiceSlug, string ServiceName, int Count);

public record ServiceAlertCountDto(string ServiceSlug, string ServiceName, int Count);

public record SeverityIncidentCountDto(string Severity, int Count);

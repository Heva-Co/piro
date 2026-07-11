namespace Piro.Application.DTOs;

/// <summary>Incident/alert response-time and volume metrics for a given date range.</summary>
public record DashboardMetricsDto(
    DateOnly From,
    DateOnly To,

    /// <summary>Mean seconds from Incident.StartDateTime to Incident.AcknowledgedAt, across incidents
    /// acknowledged within the range. Null if no incident in range has been acknowledged yet.</summary>
    double? MttaSeconds,

    /// <summary>Mean seconds from Incident.StartDateTime to Incident.EndDateTime, across incidents
    /// resolved within the range. Null if no incident in range has been resolved yet.</summary>
    double? MttrSeconds,

    /// <summary>Mean seconds from Alert.FiredAt to the CreatedAt of the Incident it was hooked to,
    /// across alerts hooked to an incident within the range. Null if none.</summary>
    double? MttdSeconds,

    /// <summary>count(Alert) / count(Alert with IncidentId != null) within the range — how many raw
    /// alerts it took to produce one incident-worthy signal. Null if no alerts fired in range.</summary>
    double? AlertNoiseRatio,

    int IncidentCount,
    int AlertCount,

    IEnumerable<DailyIncidentCountDto> DailyIncidentCounts,
    IEnumerable<ServiceIncidentCountDto> IncidentsByService,
    IEnumerable<SeverityIncidentCountDto> AlertsBySeverity
);

public record DailyIncidentCountDto(DateOnly Date, int Count);

public record ServiceIncidentCountDto(string ServiceSlug, string ServiceName, int Count);

public record SeverityIncidentCountDto(string Severity, int Count);

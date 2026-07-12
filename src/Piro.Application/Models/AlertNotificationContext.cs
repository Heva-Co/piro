using Piro.Domain.Enums;

namespace Piro.Application.Models;

/// <summary>Contextual data passed to a <see cref="Interfaces.ITriggerDispatcher"/> when an alert fires or recovers.</summary>
public record AlertNotificationContext(
    /// <summary>Name of the service that owns the check.</summary>
    string ServiceName,
    /// <summary>Name of the check that triggered the alert.</summary>
    string CheckName,
    /// <summary>Current status that caused the alert.</summary>
    ServiceStatus CurrentStatus,
    /// <summary>Human-readable description of the alert config (optional).</summary>
    string? AlertDescription,
    /// <summary>Severity level from the alert config.</summary>
    AlertSeverity Severity,
    /// <summary>True when the alert is recovering (transitioning from alerting → healthy).</summary>
    bool IsRecovery,
    DateTimeOffset FiredAt,
    int CheckId = 0,
    string? AlertValue = null,
    int FailureThreshold = 1,
    int SuccessThreshold = 1,
    string? IncidentUrl = null,
    /// <summary>Absolute admin URL to the service's detail page, if the site URL is configured.</summary>
    string? ServiceUrl = null,
    /// <summary>Absolute admin URL to the check's detail page, if the site URL is configured.</summary>
    string? CheckUrl = null,
    /// <summary>
    /// <see cref="FiredAt"/> pre-formatted for display in the recipient's own time zone, with the
    /// zone name in parentheses (e.g. "2026-07-11 14:32 (America/Bogota)"). Built per-recipient in
    /// <c>EscalationCheckerService.BuildContext</c> since each on-call user may have a different
    /// <see cref="Piro.Domain.Entities.AppUser.TimeZone"/> — never derive display time from
    /// <see cref="FiredAt"/> directly in a dispatcher/template.
    /// </summary>
    string? FiredAtDisplay = null
);

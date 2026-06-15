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
    DateTime FiredAt,
    int CheckId = 0,
    string? AlertValue = null,
    int FailureThreshold = 1,
    int SuccessThreshold = 1,
    string? IncidentUrl = null
);

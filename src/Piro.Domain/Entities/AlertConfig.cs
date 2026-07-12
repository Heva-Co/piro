using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>Rule that fires a notification when a check's metric exceeds a threshold.</summary>
public class AlertConfig
{
    public int Id { get; set; }
    public int CheckId { get; set; }
    public AlertFor AlertFor { get; set; }

    /// <summary>
    /// Threshold value: a <see cref="ServiceStatus"/> name for Status alerts,
    /// a latency in ms for Latency alerts, or an uptime percentage for Uptime alerts.
    /// </summary>
    public string AlertValue { get; set; } = string.Empty;

    /// <summary>Consecutive failures before the alert is triggered.</summary>
    public int FailureThreshold { get; set; } = 1;

    /// <summary>Consecutive successes required to auto-resolve the alert.</summary>
    public int SuccessThreshold { get; set; } = 1;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;

    /// <summary>True while the alert is in a fired state; prevents duplicate notifications.</summary>
    public bool IsAlerting { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Check Check { get; set; } = null!;
}

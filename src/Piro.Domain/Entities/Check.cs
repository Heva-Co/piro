using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>A single monitoring probe configured for a <see cref="Service"/>.</summary>
/// <remarks>
/// Every check belongs to exactly one service. Its result feeds into the parent
/// service's status computation. All checks are scheduled via Quartz cron triggers.
/// </remarks>
public class Check
{
    public int Id { get; set; }
    public int ServiceId { get; set; }

    /// <summary>URL-safe identifier, unique within its parent service.</summary>
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CheckType Type { get; set; }

    /// <summary>Cron expression (Quartz format) that controls execution frequency.</summary>
    public string Cron { get; set; } = "* * * * *";

    /// <summary>JSON blob with type-specific configuration (URL, host, port, etc.).</summary>
    public string TypeDataJson { get; set; } = "{}";

    public ServiceStatus CurrentStatus { get; set; } = ServiceStatus.NO_DATA;
    public ServiceStatus DefaultStatus { get; set; } = ServiceStatus.NO_DATA;
    public bool IsActive { get; set; } = true;

    /// <summary>Consecutive failures required before transitioning to DOWN.</summary>
    public int? FailureThreshold { get; set; }

    /// <summary>Consecutive successes required to recover from DOWN/DEGRADED.</summary>
    public int? RecoveryThreshold { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Days of status history shown on desktop. Overrides service-level setting when set.</summary>
    public int? HistoryDaysDesktop { get; set; }
    /// <summary>Days of status history shown on mobile. Overrides service-level setting when set.</summary>
    public int? HistoryDaysMobile { get; set; }

    /// <summary>
    /// When true, this check is dispatched to every connected remote worker simultaneously.
    /// Each worker executes independently and sends back its own result, enabling
    /// regional latency comparison and outage detection.
    /// When false, the check runs only on the embedded local worker.
    /// </summary>
    public bool IsMultiRegion { get; set; }

    /// <summary>Optional reference to a shared Integration (e.g. Google Cloud service account).</summary>
    public int? IntegrationId { get; set; }

    /// <summary>How critical this check is to the parent service. Determines incident impact when auto-creating incidents.</summary>
    public CheckCriticality Criticality { get; set; } = CheckCriticality.High;

    /// <summary>When true, a draft incident is automatically created when this check transitions to alerting.</summary>
    public bool AutomaticallyCreateIncident { get; set; }

    /// <summary>When true, the auto-created incident is resolved when all alerting checks on the service recover.</summary>
    public bool AutomaticallyCloseIncident { get; set; }

    public Service Service { get; set; } = null!;
    public Integration? Integration { get; set; }
    public ICollection<CheckDataPoint> DataPoints { get; set; } = [];
    public ICollection<AlertConfig> AlertConfigs { get; set; } = [];
}

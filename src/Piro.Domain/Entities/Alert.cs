using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// Persistent instance of an <see cref="AlertConfig"/> firing. Exists independently of any
/// <see cref="Incident"/> — it is hooked to one only once it crosses an incident threshold
/// (see <see cref="AlertConfig.IncidentThresholdOccurrences"/> or concurrent-alert count).
/// </summary>
public class Alert
{
    public int Id { get; set; }
    public int AlertConfigId { get; set; }
    public int CheckId { get; set; }
    public int ServiceId { get; set; }

    /// <summary>Null until this Alert is hooked to an Incident (by occurrence or concurrent-count threshold).</summary>
    public int? IncidentId { get; set; }

    /// <summary>Impact captured at fire time from <see cref="AlertConfig.Severity"/> — frozen, not re-derived later.</summary>
    public ServiceStatus ImpactAtFireTime { get; set; }

    /// <summary>Frozen error text/payload at the moment of firing (e.g. "HTTP status code 503").</summary>
    public string? Message { get; set; }

    /// <summary>Deterministic normalization of <see cref="Message"/> (trim + lowercase + collapse whitespace) —
    /// exact-match dedup key, not fuzzy similarity.</summary>
    public string MessageFingerprint { get; set; } = string.Empty;

    public DateTimeOffset FiredAt { get; set; }

    /// <summary>Null while active. Set when the underlying <see cref="AlertConfig"/> recovers.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>How many consecutive failing evaluations with the same <see cref="MessageFingerprint"/>
    /// have been folded into this row since <see cref="FiredAt"/>.</summary>
    public int OccurrenceCount { get; set; } = 1;

    public AlertConfig AlertConfig { get; set; } = null!;
    public Check Check { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public Incident? Incident { get; set; }
}

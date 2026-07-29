using Piro.Contracts;
using Piro.Domain.Enums;
using Piro.Domain.Auditing;

namespace Piro.Domain.Entities;

/// <summary>Rule that fires a notification when a check's dimension crosses a threshold.</summary>
public class AlertConfig : IAuditable
{
    public int Id { get; set; }
    public int CheckId { get; set; }

    /// <summary>
    /// The check dimension this rule watches, by its stable name (e.g. "Status", "Latency",
    /// "CertExpiry", "FailedNameServers", "LastRunAge"). Matches one of the check's declared
    /// <c>DimensionSpec.Name</c> values (see the check's manifest). Replaces the old closed
    /// <c>AlertFor</c> enum so a check can add a dimension without a core enum change.
    /// </summary>
    public string Dimension { get; set; } = string.Empty;

    /// <summary>
    /// How <see cref="AlertValue"/> is compared against the dimension's measured value.
    /// <see cref="DimensionComparison.Threshold"/> for a numeric magnitude;
    /// <see cref="DimensionComparison.Equality"/> for a categorical exact match (the Status dimension).
    /// Copied from the check's <c>DimensionSpec</c> when the rule is created, so the evaluator needs no
    /// knowledge of the check type.
    /// </summary>
    public DimensionComparison Comparison { get; set; } = DimensionComparison.Threshold;

    /// <summary>
    /// For a <see cref="DimensionComparison.Threshold"/> rule, which way is worse — copied from the
    /// check's <c>DimensionSpec</c> so the generic evaluator fires on value ≥ threshold
    /// (<see cref="ThresholdDirection.HigherIsWorse"/>) or value ≤ threshold
    /// (<see cref="ThresholdDirection.LowerIsWorse"/>) without knowing what the dimension means.
    /// Ignored for an Equality rule.
    /// </summary>
    public ThresholdDirection Direction { get; set; } = ThresholdDirection.HigherIsWorse;

    /// <summary>
    /// The value compared against the dimension's measurement: a <see cref="ServiceStatus"/> name when
    /// <see cref="Comparison"/> is Equality (Status alerts), otherwise a numeric threshold (latency ms,
    /// days-until-expiry, failed count, run-age hours) parsed at evaluation time.
    /// </summary>
    public string AlertValue { get; set; } = string.Empty;

    /// <summary>Consecutive failing cycles before the alert is triggered (the temporal axis).</summary>
    public int FailureThreshold { get; set; } = 1;

    /// <summary>Consecutive healthy cycles required to auto-resolve the alert (the temporal axis).</summary>
    public int SuccessThreshold { get; set; } = 1;

    /// <summary>
    /// Multi-region quorum (the spatial axis): the minimum number of regions that must meet this rule's
    /// condition within one execution cycle for that cycle to count as failing. Composes with
    /// <see cref="FailureThreshold"/> (cycles) and <see cref="SuccessThreshold"/> — e.g. "2 regions failing
    /// for 3 consecutive cycles". Default <c>1</c> preserves the original behaviour (any single region
    /// failing makes the cycle fail); a single-region check only ever has one region per cycle, so this has
    /// no effect there.
    /// </summary>
    public int MinFailingRegions { get; set; } = 1;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;

    /// <summary>True while the alert is in a fired state; prevents duplicate notifications.</summary>
    public bool IsAlerting { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Check Check { get; set; } = null!;
}

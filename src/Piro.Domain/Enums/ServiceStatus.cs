namespace Piro.Domain.Enums;

/// <summary>Operational status of a service or check.</summary>
/// <remarks>Priority order (highest first): MAINTENANCE &gt; DOWN &gt; PARTIALLY_DOWN &gt; DEGRADED &gt; UP &gt; NO_DATA.</remarks>
public enum ServiceStatus
{
    NO_DATA,
    UP,
    DEGRADED,

    /// <summary>
    /// A multi-region check whose target is down in some, but not all, of the regions that measured it —
    /// a regional (partial) outage rather than a full one. Only ever produced by multi-region aggregation
    /// (<c>MultiRegionBatchTracker</c>); a single-region check is only ever UP or DOWN. More severe than
    /// DEGRADED (impaired but reachable), less severe than DOWN (down everywhere).
    /// </summary>
    PARTIALLY_DOWN,

    DOWN,
    MAINTENANCE,
    /// <summary>The check executor itself threw an unexpected exception. Not a service outage — internal error only.</summary>
    FAILURE
}

namespace Piro.Contracts;

/// <summary>
/// Which way a dimension's value trends toward "worse", so the alert policy can compare a measurement
/// against a threshold generically — without knowing what the dimension means. Latency is
/// <see cref="HigherIsWorse"/> (500ms is worse than 50ms); certificate days-remaining is
/// <see cref="LowerIsWorse"/> (3 days is worse than 90). The direction travels with the data so the
/// policy needs no per-check knowledge. Only meaningful for a <see cref="DimensionComparison.Threshold"/>
/// comparison; ignored for <see cref="DimensionComparison.Equality"/>.
/// <para>Lives in Piro.Contracts so both Piro.Domain and Piro.Checks.Abstractions share the one type.</para>
/// </summary>
public enum ThresholdDirection
{
    /// <summary>Bigger values are worse — the policy fires when value ≥ threshold (latency, failed counts, age).</summary>
    HigherIsWorse,

    /// <summary>Smaller values are worse — the policy fires when value ≤ threshold (days-until-expiry, success ratio).</summary>
    LowerIsWorse,
}

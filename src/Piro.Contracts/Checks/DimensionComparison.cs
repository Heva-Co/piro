namespace Piro.Contracts;

/// <summary>
/// How the alert policy compares a dimension's measured value against a configured threshold. Kept a
/// small closed set on purpose: most dimensions are a monotonic number (<see cref="Threshold"/>), but a
/// few are categorical and only make sense as an exact match (<see cref="Equality"/> — e.g. the check's
/// Status). This is the seam that lets the evaluator stay a single generic loop instead of a per-check
/// switch, while still refusing to pretend a categorical value is a magnitude. New kinds (band/range)
/// are added here when a real check needs one, never speculatively.
/// <para>
/// Lives in Piro.Contracts (not Piro.Checks.Abstractions) so both the persisted <c>AlertConfig</c>
/// entity in Piro.Domain and the check-side <c>DimensionSpec</c> can share the one type.
/// </para>
/// </summary>
public enum DimensionComparison
{
    /// <summary>
    /// The value is a magnitude compared against a numeric threshold; the fire condition follows the
    /// dimension's <see cref="ThresholdDirection"/> (HigherIsWorse ⇒ value ≥ threshold; LowerIsWorse ⇒
    /// value ≤ threshold). This is the common case (latency, cert days, failed counts, run age).
    /// </summary>
    Threshold,

    /// <summary>
    /// The value is categorical and fires on an exact match against the configured value, ignoring
    /// <see cref="ThresholdDirection"/> entirely. Used for the check's Status (Down/Degraded/Up) — a
    /// state, not a number, so it must never be forced through a monotonic comparison.
    /// </summary>
    Equality,
}

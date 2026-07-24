using Piro.Contracts;

namespace Piro.Checks.Abstractions;

/// <summary>
/// The declaration of a check dimension: its stable name, how the policy compares it, which way is
/// worse, and an optional unit, defined once and reused. A check declares its dimensions as
/// <see cref="DimensionSpec"/> values, lists them in its <see cref="CheckManifest.Dimensions"/>, and
/// calls <see cref="Measure"/> to attach a value at probe time. Because the manifest and the probe
/// result both flow from the same spec, the dimension name cannot drift between them (no stray string
/// literals to mistype).
/// <para>
/// The alert policy binds an <c>AlertConfig.Dimension</c> to a spec's <see cref="Name"/> and evaluates
/// the measured value against its threshold using <see cref="Comparison"/> (and, for
/// <see cref="DimensionComparison.Threshold"/>, <see cref="Direction"/>), generically, with no knowledge
/// of the check type. Adding a check with a new dimension therefore never touches the policy.
/// </para>
/// </summary>
public sealed record DimensionSpec(
    string Name,
    DimensionComparison Comparison,
    ThresholdDirection Direction,
    string? Unit = null)
{
    /// <summary>Produces the measured <see cref="CheckDimension"/> for this spec — the value with the spec's declared name/direction/unit.</summary>
    public CheckDimension Measure(double value) => new(Name, value, Direction, Unit);
}

/// <summary>
/// Dimensions shared across many checks, declared once so every check reports them under the exact same
/// name/comparison/direction/unit. Check-specific dimensions (CertExpiry, FailedNameServers, …) are
/// declared by the check itself.
/// </summary>
public static class CommonDimensions
{
    /// <summary>
    /// The check's availability state (Up/Down/Degraded), compared by exact match — a category, not a
    /// magnitude. Every check reports this; the alert policy fires when Status equals the configured
    /// value. Direction is irrelevant for an equality comparison and is set to a stable default.
    /// </summary>
    public static readonly DimensionSpec Status = new("Status", DimensionComparison.Equality, ThresholdDirection.HigherIsWorse);

    /// <summary>Round-trip probe latency in milliseconds — a magnitude where higher is worse.</summary>
    public static readonly DimensionSpec Latency = new("Latency", DimensionComparison.Threshold, ThresholdDirection.HigherIsWorse, "ms");
}

namespace Piro.Checks.Abstractions;

/// <summary>
/// Everything Piro knows about a check type, declared by the check itself (RFC 0016-style). Replaces the
/// old <c>CheckType</c> enum value + <c>[CheckTypeManifest]</c> attribute pairing: the check's own class
/// is now the single place that says what it is, its config shape, and which alert dimensions apply.
/// </summary>
public sealed class CheckManifest
{
    /// <summary>Human label for the check type (e.g. "HTTP", "GCP Cloud Run Job").</summary>
    public required string Label { get; init; }

    /// <summary>One-line description shown in the check-type picker.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// The DataAnnotations-annotated config record for this check (e.g. <c>HttpCheckConfig</c>). Piro
    /// builds its config form from this and deserializes the stored config into it before running.
    /// </summary>
    public required Type ConfigType { get; init; }

    /// <summary>Default polling interval in seconds a newly-created check of this type starts with.</summary>
    public int DefaultIntervalSeconds { get; init; } = 60;

    /// <summary>
    /// The alert dimensions this check can be alerted on (Status, Latency, CertExpiry, …), each a
    /// self-describing <see cref="DimensionSpec"/> (name + how to compare + which way is worse + unit).
    /// The policy uses these to know which thresholds are meaningful and how to evaluate them, and the
    /// UI builds the alert form from them, without any dependency on a Piro-side alert catalog enum.
    /// A check reuses the shared <see cref="CommonDimensions"/> specs (Status, Latency) and declares any
    /// check-specific ones itself, so the same spec object feeds both this manifest and the probe result.
    /// </summary>
    public IReadOnlyList<DimensionSpec> Dimensions { get; init; } = [];

    /// <summary>
    /// The id of a provider integration this check requires before it can run (e.g. "GoogleCloud" for the
    /// Cloud Run Job check, which resolves that integration's token provider via the host). Null when the
    /// check needs none. The check declares it here — its own manifest is the single source of truth — so
    /// the UI can require the integration up front and Piro never hardcodes the check→integration link.
    /// </summary>
    public string? RequiredIntegration { get; init; }
}

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

    /// <summary>
    /// Whether this check's probe reads its own past data points instead of (or as well as) making a
    /// network call (RFC 0013 — the Heartbeat check reads "when was I last seen"). When true, the probe
    /// may resolve an <see cref="IOwnCheckPoints"/> through the host — a reader already scoped to the
    /// check currently executing, so it returns only that check's data points and the probe never sees a
    /// check id, the <c>Check</c> entity, a repository, or any other check's data. Declared, not
    /// name-matched: Piro's executor adapter wires the scoped reader only for a check that sets this flag,
    /// so the "checks know nothing" boundary is untouched for every check that leaves it false (the
    /// default). More general than a precomputed "last seen": a future push-based check can average the
    /// recent points or detect a pattern, not just read the newest.
    /// </summary>
    public bool ConsumesCheckPoints { get; init; }

    /// <summary>
    /// Whether a check of this type must run in a single region (multi-region is rejected). A check
    /// declares this itself — Piro never infers it from another capability. The Heartbeat check sets it
    /// because its pings are ingested and evaluated in one place, so fanning it across regions is
    /// meaningless; a normal outbound probe leaves it false and may run multi-region. Independent of
    /// <see cref="ConsumesCheckPoints"/>: reading one's own history does not, by itself, pin a check to a
    /// region (the data points live in the shared store).
    /// </summary>
    public bool SingleRegionOnly { get; init; }
}

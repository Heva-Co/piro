namespace Piro.Checks.Abstractions;

/// <summary>
/// The raw outcome of one probe. A check answers only "did the probe succeed, and here are my
/// measurements" — it never decides severity. Turning an outcome + dimensions into UP/DEGRADED/DOWN is
/// the alert policy's job, not the check's (RFC 0016-style separation for checks).
/// </summary>
public enum CheckOutcome
{
    /// <summary>The probe succeeded (reached the target and it responded acceptably).</summary>
    Up,

    /// <summary>The probe ran but the target failed the check (unreachable, bad status, timed out, expired, …).</summary>
    Down,

    /// <summary>The check itself failed to run (misconfigured, executor threw) — not a target outage. The policy treats this apart from a real Down.</summary>
    Error,
}

/// <summary>
/// What a check produces: a raw <see cref="Outcome"/> plus the set of self-describing
/// <see cref="Dimensions"/> the alert policy evaluates. Deliberately carries no <c>ServiceStatus</c> —
/// no UP/DEGRADED/DOWN here; the policy derives that from <see cref="Outcome"/> and by comparing each
/// dimension's value against its threshold (using the dimension's own <see cref="ThresholdDirection"/>).
/// <para>
/// A check with several simultaneous metrics returns several dimensions (e.g. GCP Cloud Run Job:
/// last-run age AND failed-task count). A check with one measurable returns one (SSL: cert days). The
/// policy stays a single generic evaluator — it never switches on check type.
/// </para>
/// </summary>
public sealed record CheckProbeResult(
    CheckOutcome Outcome,
    /// <summary>The measurements this probe produced, one per metric. Empty for a check with nothing to measure (or an Error).</summary>
    IReadOnlyList<CheckDimension> Dimensions,
    /// <summary>Human-readable detail for a Down/Error (the failure reason). Null on a clean Up.</summary>
    string? Message = null)
{
    private static readonly IReadOnlyList<CheckDimension> None = [];

    /// <summary>A successful probe with its measured dimensions (e.g. latency, cert-days).</summary>
    public static CheckProbeResult Ok(params CheckDimension[] dimensions) =>
        new(CheckOutcome.Up, dimensions.Length == 0 ? None : dimensions);

    /// <summary>The target failed the check (a real "service is down" signal), optionally with the dimensions measured up to the failure.</summary>
    public static CheckProbeResult DownWith(string message, params CheckDimension[] dimensions) =>
        new(CheckOutcome.Down, dimensions.Length == 0 ? None : dimensions, message);

    /// <summary>The check couldn't run (config/executor error) — not a target outage.</summary>
    public static CheckProbeResult Failed(string message) =>
        new(CheckOutcome.Error, None, message);
}

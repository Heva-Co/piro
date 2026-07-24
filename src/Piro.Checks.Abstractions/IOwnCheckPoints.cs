namespace Piro.Checks.Abstractions;

/// <summary>
/// A probe's read-only window onto <b>its own</b> past data points (RFC 0013). Resolved through the
/// <see cref="ICheckHost"/> only by a check whose manifest declares
/// <see cref="CheckManifest.ConsumesCheckPoints"/>; Piro's executor adapter binds an instance already
/// scoped to the check currently executing, so every method returns only that check's points. The probe
/// therefore never learns its check id, never sees the <c>Check</c> entity or a repository, and can
/// never reach another check's data — the "checks know nothing about Piro" boundary holds even though
/// the check now reads history. Push-based checks use this instead of an outbound network call: a
/// Heartbeat reads <see cref="LatestAsync"/> ("when was I last pinged"); a future check could average
/// <see cref="RecentAsync"/>.
/// </summary>
public interface IOwnCheckPoints
{
    /// <summary>The most recent point this check recorded, or null if it has none yet.</summary>
    Task<CheckPoint?> LatestAsync(CancellationToken ct = default);

    /// <summary>The newest <paramref name="limit"/> points this check recorded, most-recent first.</summary>
    Task<IReadOnlyList<CheckPoint>> RecentAsync(int limit, CancellationToken ct = default);
}

/// <summary>
/// One past data point of a check, in neutral terms a probe can read without depending on Piro's Domain
/// entities: when it was recorded (unix seconds), its status string, and its measured dimensions by name.
/// </summary>
public sealed record CheckPoint(
    long Timestamp,
    string Status,
    IReadOnlyDictionary<string, double> Dimensions);

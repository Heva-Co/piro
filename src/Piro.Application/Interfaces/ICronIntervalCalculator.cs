namespace Piro.Application.Interfaces;

/// <summary>
/// Derives the effective interval of a cron expression — the spacing between consecutive fires.
/// Implemented in Infrastructure over Quartz's cron engine (RFC 0011); kept behind an interface so
/// Application-layer validation (interval floors, timeout &lt; interval) has no Quartz dependency.
/// </summary>
public interface ICronIntervalCalculator
{
    /// <summary>
    /// The smallest gap between consecutive fires of <paramref name="cron"/> over a sampling window
    /// — the tightest cadence the schedule can produce (an irregular cron like "0 0 9,17 * * ?" has
    /// unequal gaps; the floor must hold against the smallest). Returns null if the cron is invalid
    /// or fires too rarely to sample within the window.
    /// </summary>
    TimeSpan? SmallestInterval(string cron);

    /// <summary>
    /// Whether <paramref name="cron"/> parses as a valid schedule. Distinct from
    /// <see cref="SmallestInterval"/> returning null, which conflates "malformed" with "fires too
    /// rarely to sample". Lets the Application layer reject an invalid cron before persisting it,
    /// rather than throwing from the scheduler after the commit (RFC 0019 §4.3).
    /// </summary>
    bool IsValid(string cron);
}

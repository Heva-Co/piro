using Piro.Contracts;

namespace Piro.Checks;

/// <summary>
/// Configuration for a push-based Heartbeat check (RFC 0013). The monitored target pings Piro on a
/// schedule; the check goes DOWN when a ping is overdue. There is no URL — the target drives the ping.
/// </summary>
public record HeartbeatCheckConfig
{
    /// <summary>
    /// How often a ping is expected, in seconds — the target's cadence. "Overdue" is
    /// <c>now − lastSeen &gt; ExpectedIntervalSeconds + GracePeriodSeconds</c>. The user sets their pinger
    /// and this field to the same value.
    /// </summary>
    [ConfigField("Expected interval (seconds)", HelpText = "How often the target is expected to ping. A missed ping beyond this + grace marks the check DOWN.")]
    public int ExpectedIntervalSeconds { get; init; } = 60;

    /// <summary>
    /// Slack beyond the expected interval to absorb network jitter and clock drift before a tick reports
    /// DOWN. Tolerance for clock noise — how many consecutive overdue ticks constitute an outage is the
    /// alert config's <c>FailureThreshold</c>, not duplicated here.
    /// </summary>
    [ConfigField("Grace period (seconds)", HelpText = "Extra slack beyond the expected interval before a missed ping counts as DOWN.")]
    public int GracePeriodSeconds { get; init; } = 30;
}

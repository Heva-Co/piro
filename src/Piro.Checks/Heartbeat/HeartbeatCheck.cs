using Piro.Checks.Abstractions;

namespace Piro.Checks;

/// <summary>
/// A push-based liveness check (RFC 0013): instead of probing a target, the target pings Piro, and this
/// check's probe reads "when was I last pinged" from its own data points and reports DOWN when the ping
/// is overdue. It is an ordinary <see cref="ICheck"/> that rides the normal Quartz tick — the staleness
/// evaluation IS the tick — so there is no separate sweep service. It reports raw state only (Up/Down);
/// how many overdue ticks page is the alert policy's FailureThreshold. Before the first ping ever, it is
/// NO_DATA (an unconfigured sender), not an outage.
/// </summary>
public sealed class HeartbeatCheck : Check<HeartbeatCheckConfig>
{
    public override string CheckId => "Heartbeat";

    public override CheckManifest Manifest => new()
    {
        Label = "Heartbeat",
        Description = "The monitored target pings Piro on a schedule; a missed ping marks the check DOWN.",
        ConfigType = typeof(HeartbeatCheckConfig),
        Dimensions = [CommonDimensions.Status],
        // Reads its own past points (the last ping) through the host-provided scoped reader.
        ConsumesCheckPoints = true,
        // Pings are ingested and evaluated in one place, so a heartbeat can't be fanned across regions.
        SingleRegionOnly = true,
    };

    public override async Task<CheckProbeResult> ProbeAsync(HeartbeatCheckConfig config, ICheckHost host, CancellationToken ct = default)
    {
        var points = host.GetRequiredService<IOwnCheckPoints>();
        var last = await points.LatestAsync(ct);

        // No ping ever received — the sender isn't wired up yet, not an outage. Error → NO_DATA, non-alerting.
        if (last is null)
            return CheckProbeResult.Failed("No heartbeat received yet.");

        var ageSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - last.Timestamp;
        var window = config.ExpectedIntervalSeconds + config.GracePeriodSeconds;

        return ageSeconds <= window
            ? CheckProbeResult.Ok()
            : CheckProbeResult.DownWith($"No heartbeat in {ageSeconds}s (expected within {window}s).");
    }

    public override ICheckInboundHandler? ProvidedInboundHandler() => new HeartbeatInboundHandler();
}

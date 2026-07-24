using System.Net.NetworkInformation;
using Piro.Checks.Abstractions;

namespace Piro.Checks;

/// <summary>Probes a host with an ICMP echo. Up on a successful reply; Down otherwise.</summary>
public sealed class PingCheck : Check<PingCheckConfig>
{
    public override string CheckId => "Ping";

    public override CheckManifest Manifest => new()
    {
        Label = "Ping",
        Description = "Send an ICMP echo to a host.",
        ConfigType = typeof(PingCheckConfig),
        Dimensions = [CommonDimensions.Status, CommonDimensions.Latency],
    };

    public override async Task<CheckProbeResult> ProbeAsync(PingCheckConfig config, ICheckHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
            return CheckProbeResult.Failed("Host is not configured.");

        using var ping = new Ping();
        try
        {
            var reply = await ping.SendPingAsync(config.Host, config.TimeoutMs);
            return reply.Status == IPStatus.Success
                ? CheckProbeResult.Ok(CommonDimensions.Latency.Measure(reply.RoundtripTime))
                : CheckProbeResult.DownWith($"Ping failed: {reply.Status}");
        }
        catch (Exception ex)
        {
            return CheckProbeResult.DownWith(ex.Message);
        }
    }
}

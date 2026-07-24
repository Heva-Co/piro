using System.Diagnostics;
using System.Net.Sockets;
using Piro.Checks.Abstractions;

namespace Piro.Checks;

/// <summary>Probes TCP port connectivity. Up if the connection opens; Down if it times out or is refused.</summary>
public sealed class TcpCheck : Check<TcpCheckConfig>
{
    public override string CheckId => "TCP";

    public override CheckManifest Manifest => new()
    {
        Label = "TCP",
        Description = "Open a TCP connection to a host and port.",
        ConfigType = typeof(TcpCheckConfig),
        Dimensions = [CommonDimensions.Status, CommonDimensions.Latency],
    };

    public override async Task<CheckProbeResult> ProbeAsync(TcpCheckConfig config, ICheckHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.Host) || config.Port <= 0)
            return CheckProbeResult.Failed("Host or port is not configured.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(config.TimeoutMs);

        var sw = Stopwatch.StartNew();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(config.Host, config.Port, cts.Token);
            sw.Stop();
            return CheckProbeResult.Ok(Latency(sw));
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return CheckProbeResult.DownWith("Connection timed out.", Latency(sw));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return CheckProbeResult.DownWith(ex.Message, Latency(sw));
        }
    }

    private static CheckDimension Latency(Stopwatch sw) =>
        CommonDimensions.Latency.Measure(sw.Elapsed.TotalMilliseconds);
}

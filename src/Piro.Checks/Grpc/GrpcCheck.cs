using System.Diagnostics;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Piro.Checks.Abstractions;

namespace Piro.Checks;

/// <summary>
/// Probes a gRPC server via the standard health checking protocol (<c>grpc.health.v1.Health/Check</c>).
/// SERVING is Up; any other serving status is Down. If the server doesn't implement the health service:
/// a config with no Service is a generic reachability probe (Up), but a config naming a specific Service
/// asked for that service's health, which couldn't be verified (Down). The gRPC SDK owns its own
/// transport, so this check needs no HttpClient from the host.
/// </summary>
public sealed class GrpcCheck : Check<GrpcCheckConfig>
{
    public override string CheckId => "GRPC";

    public override CheckManifest Manifest => new()
    {
        Label = "gRPC",
        Description = "Probe a gRPC server via the standard health checking protocol.",
        ConfigType = typeof(GrpcCheckConfig),
        Dimensions = [CommonDimensions.Status, CommonDimensions.Latency],
    };

    public override async Task<CheckProbeResult> ProbeAsync(GrpcCheckConfig config, ICheckHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.Host) || config.Port <= 0)
            return CheckProbeResult.Failed("Host or port is not configured.");

        var scheme = config.Tls ? "https" : "http";
        var address = $"{scheme}://{config.Host}:{config.Port}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(config.TimeoutMs);

        var sw = Stopwatch.StartNew();
        try
        {
            var channelOptions = new GrpcChannelOptions();
            if (!config.Tls)
            {
                channelOptions.HttpClient = new HttpClient(new SocketsHttpHandler())
                {
                    DefaultRequestVersion = System.Net.HttpVersion.Version20,
                    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
                };
                channelOptions.DisposeHttpClient = true;
            }

            using var channel = GrpcChannel.ForAddress(address, channelOptions);
            var client = new Health.HealthClient(channel);
            var response = await client.CheckAsync(
                new HealthCheckRequest { Service = config.Service ?? string.Empty },
                cancellationToken: cts.Token);
            sw.Stop();

            return response.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? CheckProbeResult.Ok(Latency(sw))
                : CheckProbeResult.DownWith($"gRPC health status: {response.Status}.", Latency(sw));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
        {
            sw.Stop();
            return string.IsNullOrWhiteSpace(config.Service)
                ? CheckProbeResult.Ok(Latency(sw)) with { Message = "Server reachable; health checking protocol not implemented." }
                : CheckProbeResult.DownWith(
                    $"Server does not implement the health checking protocol, so '{config.Service}' health could not be verified.",
                    Latency(sw));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            sw.Stop();
            return CheckProbeResult.DownWith("gRPC call timed out.", Latency(sw));
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            sw.Stop();
            return CheckProbeResult.DownWith("gRPC call timed out.", Latency(sw));
        }
        catch (RpcException ex)
        {
            sw.Stop();
            return CheckProbeResult.DownWith(ex.Status.Detail, Latency(sw));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return CheckProbeResult.Failed($"Executor error: {ex.Message}");
        }
    }

    private static CheckDimension Latency(Stopwatch sw) =>
        CommonDimensions.Latency.Measure(sw.Elapsed.TotalMilliseconds);
}

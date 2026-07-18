using System.Diagnostics;
using System.Text.Json;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Checks.Config;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Checks;

/// <summary>
/// Probes a gRPC server via the standard health checking protocol
/// (<c>grpc.health.v1.Health/Check</c>). A <c>SERVING</c> response is UP; any other serving status is
/// DOWN. When the server doesn't implement the health service (<see cref="StatusCode.Unimplemented"/>)
/// the outcome depends on intent: a config with no <c>Service</c> is a generic reachability probe, so a
/// server that answered on the wire is UP; but a config that named a specific <c>Service</c> asked for
/// that service's health — which couldn't be verified — so it is DOWN rather than a false positive.
/// </summary>
internal class GrpcCheckExecutor : ICheckExecutor
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public CheckType CheckType => CheckType.GRPC;

    public async Task<CheckExecutionResult> ExecuteAsync(Check check, CancellationToken ct = default)
    {
        try
        {
            var data = JsonSerializer.Deserialize<GrpcCheckConfig>(check.TypeDataJson, _json)
                       ?? new GrpcCheckConfig();

            if (string.IsNullOrWhiteSpace(data.Host) || data.Port <= 0)
                return new CheckExecutionResult(ServiceStatus.FAILURE, null, "Host or port is not configured.");

            var scheme = data.Tls ? "https" : "http";
            var address = $"{scheme}://{data.Host}:{data.Port}";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(data.TimeoutMs);

            var sw = Stopwatch.StartNew();
            try
            {
                // For a plaintext (h2c) server we must speak HTTP/2 with prior knowledge: the client
                // otherwise tries to negotiate TLS over an http:// address and fails with a frame/SSL
                // error. Forcing the exact HTTP/2 version on the client skips that negotiation.
                var channelOptions = new GrpcChannelOptions();
                if (!data.Tls)
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
                var request = new HealthCheckRequest { Service = data.Service ?? string.Empty };

                var response = await client.CheckAsync(request, cancellationToken: cts.Token);
                sw.Stop();

                return response.Status == HealthCheckResponse.Types.ServingStatus.Serving
                    ? new CheckExecutionResult(ServiceStatus.UP, sw.Elapsed.TotalMilliseconds, null)
                    : new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds,
                        $"gRPC health status: {response.Status}.");
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
            {
                sw.Stop();
                // No named service: this is a generic reachability probe, and the server answered — UP.
                // A named service: the caller wanted that service's health, which we couldn't verify — DOWN.
                return string.IsNullOrWhiteSpace(data.Service)
                    ? new CheckExecutionResult(ServiceStatus.UP, sw.Elapsed.TotalMilliseconds,
                        "Server reachable; health checking protocol not implemented.")
                    : new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds,
                        $"Server does not implement the health checking protocol, so '{data.Service}' health could not be verified.");
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
            {
                sw.Stop();
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, "gRPC call timed out.");
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                sw.Stop();
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, "gRPC call timed out.");
            }
            catch (RpcException ex)
            {
                sw.Stop();
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, ex.Status.Detail);
            }
        }
        catch (Exception ex)
        {
            return new CheckExecutionResult(ServiceStatus.FAILURE, null, $"Executor error: {ex.Message}");
        }
    }
}

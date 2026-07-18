using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Checks.Config;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Checks;

/// <summary>Executes a TCP port connectivity check.</summary>
internal class TcpCheckExecutor : ICheckExecutor
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public CheckType CheckType => CheckType.TCP;

    public async Task<CheckExecutionResult> ExecuteAsync(Check check, CancellationToken ct = default)
    {
        try
        {
            var data = JsonSerializer.Deserialize<TcpCheckConfig>(check.TypeDataJson, _json)
                       ?? new TcpCheckConfig();

            if (string.IsNullOrWhiteSpace(data.Host) || data.Port <= 0)
                return new CheckExecutionResult(ServiceStatus.FAILURE, null, "Host or port is not configured.");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(data.TimeoutMs);

            var sw = Stopwatch.StartNew();
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(data.Host, data.Port, cts.Token);
                sw.Stop();
                return new CheckExecutionResult(ServiceStatus.UP, sw.Elapsed.TotalMilliseconds, null);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, "Connection timed out.");
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, ex.Message);
            }
        }
        catch (Exception ex)
        {
            return new CheckExecutionResult(ServiceStatus.FAILURE, null, $"Executor error: {ex.Message}");
        }
    }
}

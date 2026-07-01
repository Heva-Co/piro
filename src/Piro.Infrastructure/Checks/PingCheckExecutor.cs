using System.Net.NetworkInformation;
using System.Text.Json;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Models.TypeData;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Checks;

/// <summary>Executes an ICMP ping check.</summary>
internal class PingCheckExecutor : ICheckExecutor
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public CheckType CheckType => CheckType.Ping;

    public async Task<CheckExecutionResult> ExecuteAsync(Check check, CancellationToken ct = default)
    {
        try
        {
            var data = JsonSerializer.Deserialize<PingCheckData>(check.TypeDataJson, _json)
                       ?? new PingCheckData();

            if (string.IsNullOrWhiteSpace(data.Host))
                return new CheckExecutionResult(ServiceStatus.FAILURE, null, "Host is not configured.");

            using var ping = new Ping();
            try
            {
                var reply = await ping.SendPingAsync(data.Host, data.TimeoutMs);

                return reply.Status == IPStatus.Success
                    ? new CheckExecutionResult(ServiceStatus.UP, reply.RoundtripTime, null)
                    : new CheckExecutionResult(ServiceStatus.DOWN, null, $"Ping failed: {reply.Status}");
            }
            catch (Exception ex)
            {
                return new CheckExecutionResult(ServiceStatus.DOWN, null, ex.Message);
            }
        }
        catch (Exception ex)
        {
            return new CheckExecutionResult(ServiceStatus.FAILURE, null, $"Executor error: {ex.Message}");
        }
    }
}

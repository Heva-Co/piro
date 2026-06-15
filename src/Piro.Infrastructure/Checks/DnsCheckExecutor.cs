using System.Diagnostics;
using System.Net;
using System.Text.Json;
using DnsClient;
using DnsClient.Protocol;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Models.TypeData;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Checks;

/// <summary>Executes a DNS resolution check using DnsClient.</summary>
internal class DnsCheckExecutor : ICheckExecutor
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public CheckType CheckType => CheckType.DNS;

    public async Task<CheckExecutionResult> ExecuteAsync(Check check, CancellationToken ct = default)
    {
        var data = JsonSerializer.Deserialize<DnsCheckData>(check.TypeDataJson, _json)
                   ?? new DnsCheckData();

        if (string.IsNullOrWhiteSpace(data.Host))
            return new CheckExecutionResult(ServiceStatus.DOWN, null, "Host is not configured.");

        LookupClient client;
        if (!string.IsNullOrWhiteSpace(data.NameServer) && IPAddress.TryParse(data.NameServer, out var ns))
            client = new LookupClient(ns);
        else
            client = new LookupClient();

        var queryType = Enum.TryParse<QueryType>(data.RecordType, ignoreCase: true, out var qt) ? qt : QueryType.A;

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await client.QueryAsync(data.Host, queryType, cancellationToken: ct);
            sw.Stop();

            if (result.HasError)
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, result.ErrorMessage);

            if (!result.Answers.Any())
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, "No DNS records returned.");

            if (!string.IsNullOrWhiteSpace(data.ExpectedIp))
            {
                var addresses = result.Answers.OfType<ARecord>().Select(r => r.Address.ToString())
                    .Concat(result.Answers.OfType<AaaaRecord>().Select(r => r.Address.ToString()));

                if (!addresses.Any(a => a == data.ExpectedIp))
                    return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds,
                        $"Expected IP {data.ExpectedIp} not found in response.");
            }

            return new CheckExecutionResult(ServiceStatus.UP, sw.Elapsed.TotalMilliseconds, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }
}

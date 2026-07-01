using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
        try
        {
        return await ExecuteInternalAsync(check, ct);
        }
        catch (Exception ex)
        {
            return new CheckExecutionResult(ServiceStatus.FAILURE, null, $"Executor error: {ex.Message}");
        }
    }

    private async Task<CheckExecutionResult> ExecuteInternalAsync(Check check, CancellationToken ct)
    {
        var data = JsonSerializer.Deserialize<DnsCheckData>(check.TypeDataJson, _json)
                   ?? new DnsCheckData();

        if (string.IsNullOrWhiteSpace(data.Host))
            return new CheckExecutionResult(ServiceStatus.DOWN, null, "Host is not configured.");

        var queryType = Enum.TryParse<QueryType>(data.RecordType, ignoreCase: true, out var qt) ? qt : QueryType.A;

        // Validate ExpectedValue format before querying
        if (!string.IsNullOrWhiteSpace(data.ExpectedValue))
        {
            var validationError = ValidateExpectedValue(data.ExpectedValue, data.RecordType);
            if (validationError is not null)
                return new CheckExecutionResult(ServiceStatus.FAILURE, null,
                    $"Invalid expected value for record type {data.RecordType}: {validationError}");
        }

        var nameServers = (data.NameServers ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        // Validate name server entries
        var invalidNs = nameServers.Where(ns => !IsValidNameServer(ns)).ToList();
        if (invalidNs.Count > 0)
            return new CheckExecutionResult(ServiceStatus.FAILURE, null,
                $"Invalid name server(s): {string.Join(", ", invalidNs)}. Must be a valid IP address or hostname.");

        // Single query (system resolver)
        if (nameServers.Count == 0)
        {
            var single = await QuerySingleAsync(new LookupClient(), data.Host, queryType, data.ExpectedValue, data.RecordType, ct);
            return ApplyLatencyThresholds(single, data.DegradedLatencyMs, data.DownLatencyMs);
        }

        // Parallel queries across all name servers
        var tasks = nameServers.Select(ns => QueryNameServerAsync(ns, data.Host, queryType, data.ExpectedValue, data.RecordType, ct)).ToList();
        var results = await Task.WhenAll(tasks);

        var failures = results.Count(r => r.Status != ServiceStatus.UP);
        var maxLatency = results.Max(r => r.LatencyMs);
        var degradedAfter = data.DegradedAfter ?? 1;
        var downAfter = data.DownAfter ?? nameServers.Count;

        if (failures == 0)
        {
            var up = new CheckExecutionResult(ServiceStatus.UP, maxLatency, null);
            return ApplyLatencyThresholds(up, data.DegradedLatencyMs, data.DownLatencyMs);
        }

        var failureMessages = results
            .Select((r, i) => r.Status != ServiceStatus.UP ? $"{nameServers[i]}: {r.ErrorMessage}" : null)
            .Where(m => m is not null);
        var errorMessage = string.Join("; ", failureMessages);

        if (failures >= downAfter)
            return new CheckExecutionResult(ServiceStatus.DOWN, maxLatency, errorMessage);

        if (failures >= degradedAfter)
            return new CheckExecutionResult(ServiceStatus.DEGRADED, maxLatency, errorMessage);

        return new CheckExecutionResult(ServiceStatus.UP, maxLatency, null);
    }

    private static CheckExecutionResult ApplyLatencyThresholds(
        CheckExecutionResult result, int? degradedLatencyMs, int? downLatencyMs)
    {
        if (result.Status != ServiceStatus.UP || result.LatencyMs is null)
            return result;

        if (downLatencyMs.HasValue && result.LatencyMs >= downLatencyMs.Value)
            return new CheckExecutionResult(ServiceStatus.DOWN, result.LatencyMs,
                $"Latency {result.LatencyMs:F0} ms exceeded DOWN threshold of {downLatencyMs} ms.");

        if (degradedLatencyMs.HasValue && result.LatencyMs >= degradedLatencyMs.Value)
            return new CheckExecutionResult(ServiceStatus.DEGRADED, result.LatencyMs,
                $"Latency {result.LatencyMs:F0} ms exceeded DEGRADED threshold of {degradedLatencyMs} ms.");

        return result;
    }

    private static async Task<CheckExecutionResult> QueryNameServerAsync(
        string nameServer, string host, QueryType queryType, string? expectedValue, string recordType, CancellationToken ct)
    {
        LookupClient client;
        if (IPAddress.TryParse(nameServer, out var ip))
            client = new LookupClient(ip);
        else
        {
            // Resolve hostname to IP first using system DNS, then use that IP as NS
            var resolved = await Dns.GetHostAddressesAsync(nameServer, ct);
            client = resolved.Length > 0
                ? new LookupClient(resolved[0])
                : new LookupClient();
        }

        return await QuerySingleAsync(client, host, queryType, expectedValue, recordType, ct);
    }

    private static async Task<CheckExecutionResult> QuerySingleAsync(
        LookupClient client, string host, QueryType queryType, string? expectedValue, string recordType, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await client.QueryAsync(host, queryType, cancellationToken: ct);
            sw.Stop();

            if (result.HasError)
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, result.ErrorMessage);

            if (!result.Answers.Any())
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, "No DNS records returned.");

            if (!string.IsNullOrWhiteSpace(expectedValue))
            {
                var matchError = CheckExpectedValue(result, expectedValue, recordType);
                if (matchError is not null)
                    return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, matchError);
            }

            return new CheckExecutionResult(ServiceStatus.UP, sw.Elapsed.TotalMilliseconds, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }

    private static string? CheckExpectedValue(IDnsQueryResponse result, string expectedValue, string recordType)
    {
        var upper = recordType.ToUpperInvariant();
        return upper switch
        {
            "A" => result.Answers.OfType<ARecord>().Any(r => r.Address.ToString() == expectedValue)
                ? null
                : $"Expected IP {expectedValue} not found in A records.",

            "AAAA" => result.Answers.OfType<AaaaRecord>().Any(r => r.Address.ToString() == expectedValue)
                ? null
                : $"Expected IP {expectedValue} not found in AAAA records.",

            "CNAME" => result.Answers.OfType<CNameRecord>()
                .Any(r => r.CanonicalName.Value.TrimEnd('.').Equals(expectedValue.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
                ? null
                : $"Expected CNAME target {expectedValue} not found.",

            _ => null // unsupported types: any resolution = UP
        };
    }

    private static string? ValidateExpectedValue(string value, string recordType) =>
        recordType.ToUpperInvariant() switch
        {
            "A" => IPAddress.TryParse(value, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork
                ? null
                : "must be a valid IPv4 address.",

            "AAAA" => IPAddress.TryParse(value, out var ip6) && ip6.AddressFamily == AddressFamily.InterNetworkV6
                ? null
                : "must be a valid IPv6 address.",

            "CNAME" => IsValidHostname(value)
                ? null
                : "must be a valid hostname or FQDN.",

            _ => null
        };

    private static bool IsValidNameServer(string value)
    {
        if (IPAddress.TryParse(value, out _)) return true;
        return IsValidHostname(value);
    }

    private static bool IsValidHostname(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var host = value.TrimEnd('.');
        return Uri.CheckHostName(host) != UriHostNameType.Unknown;
    }
}

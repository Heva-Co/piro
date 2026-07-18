using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using DnsClient;
using DnsClient.Protocol;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Checks.Config;
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
        var data = JsonSerializer.Deserialize<DnsCheckConfig>(check.TypeDataJson, _json)
                   ?? new DnsCheckConfig();

        if (string.IsNullOrWhiteSpace(data.Host))
            return new CheckExecutionResult(ServiceStatus.FAILURE, null, "Host is not configured.");

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
            return await QuerySingleAsync(new LookupClient(), data.Host, queryType, data.ExpectedValue, data.RecordType, ct);

        // Parallel queries across all name servers
        var tasks = nameServers.Select(ns => QueryNameServerAsync(ns, data.Host, queryType, data.ExpectedValue, data.RecordType, ct)).ToList();
        var results = await Task.WhenAll(tasks);

        return ClassifyNsResults(results, nameServers, data);
    }

    /// <summary>
    /// Aggregates per-name-server results. DOWN only when every configured name server failed
    /// to resolve — any partial failure is reported as UP with the failure count in
    /// <see cref="CheckExecutionResult.MetricValue"/>, so severity (e.g. "alert if 1+ NS fails")
    /// is an <see cref="Piro.Domain.Entities.AlertConfig"/> decision, not the check's own (RFC 0002).
    /// </summary>
    internal static CheckExecutionResult ClassifyNsResults(
        CheckExecutionResult[] results, List<string> nameServers, DnsCheckConfig data)
    {
        var failures = results.Count(r => r.Status != ServiceStatus.UP);
        var maxLatency = results.Max(r => r.LatencyMs);

        if (failures == 0)
            return new CheckExecutionResult(ServiceStatus.UP, maxLatency, null, MetricValue: 0);

        var failureMessages = results
            .Select((r, i) => r.Status != ServiceStatus.UP ? $"{nameServers[i]}: {r.ErrorMessage}" : null)
            .Where(m => m is not null);
        var errorMessage = string.Join("; ", failureMessages);

        if (failures == nameServers.Count)
            return new CheckExecutionResult(ServiceStatus.DOWN, maxLatency, errorMessage, MetricValue: failures);

        return new CheckExecutionResult(ServiceStatus.UP, maxLatency, errorMessage, MetricValue: failures);
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

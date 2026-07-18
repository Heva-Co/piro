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
            return await QuerySingleAsync(new LookupClient(), data.Host, queryType, data, ct);

        // Parallel queries across all name servers
        var tasks = nameServers.Select(ns => QueryNameServerAsync(ns, data.Host, queryType, data, ct)).ToList();
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
        string nameServer, string host, QueryType queryType, DnsCheckConfig data, CancellationToken ct)
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

        return await QuerySingleAsync(client, host, queryType, data, ct);
    }

    private static async Task<CheckExecutionResult> QuerySingleAsync(
        LookupClient client, string host, QueryType queryType, DnsCheckConfig data, CancellationToken ct)
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

            var matchError = CheckExpectedValue(result, data);
            if (matchError is not null)
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, matchError);

            return new CheckExecutionResult(ServiceStatus.UP, sw.Elapsed.TotalMilliseconds, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }

    /// <summary>
    /// Compares a query response against the configured expectations. MX uses the structured
    /// <see cref="DnsCheckConfig.ExpectedMxRecords"/>; every other type uses the scalar
    /// <see cref="DnsCheckConfig.ExpectedValue"/>. Returns null when nothing is configured (any
    /// resolution is UP) or when the expectation is satisfied, otherwise a human-readable error.
    /// The actual comparison is delegated to pure, unit-testable helpers that take plain strings.
    /// </summary>
    private static string? CheckExpectedValue(IDnsQueryResponse result, DnsCheckConfig data)
    {
        var recordType = data.RecordType.ToUpperInvariant();

        if (recordType == "MX")
        {
            var expected = (data.ExpectedMxRecords ?? [])
                .Where(m => !string.IsNullOrWhiteSpace(m.Exchange))
                .ToList();
            if (expected.Count == 0)
                return null; // any successful MX resolution is UP

            var actual = result.Answers.OfType<MxRecord>()
                .Select(r => (Exchange: r.Exchange.Value, Priority: (int)r.Preference))
                .ToList();
            return MatchMxRecords(expected, actual);
        }

        if (string.IsNullOrWhiteSpace(data.ExpectedValue))
            return null; // any successful resolution is UP

        var values = ExtractValues(result, recordType);
        return MatchScalar(recordType, data.ExpectedValue, values);
    }

    /// <summary>
    /// Projects a response's answers into the comparable string values for a given record type.
    /// Trailing dots are stripped for name-valued records so <c>mx1.example.com</c> and
    /// <c>mx1.example.com.</c> compare equal. TXT flattens every text string across all answers.
    /// Unknown/other types fall back to <see cref="DnsResourceRecord.ToString"/> of each answer.
    /// </summary>
    internal static IReadOnlyList<string> ExtractValues(IDnsQueryResponse result, string recordType) =>
        recordType.ToUpperInvariant() switch
        {
            "A" => result.Answers.OfType<ARecord>().Select(r => r.Address.ToString()).ToList(),
            "AAAA" => result.Answers.OfType<AaaaRecord>().Select(r => r.Address.ToString()).ToList(),
            "CNAME" => result.Answers.OfType<CNameRecord>().Select(r => r.CanonicalName.Value.TrimEnd('.')).ToList(),
            "NS" => result.Answers.OfType<NsRecord>().Select(r => r.NSDName.Value.TrimEnd('.')).ToList(),
            "PTR" => result.Answers.OfType<PtrRecord>().Select(r => r.PtrDomainName.Value.TrimEnd('.')).ToList(),
            "TXT" => result.Answers.OfType<TxtRecord>().SelectMany(r => r.EscapedText).ToList(),
            _ => result.Answers.Select(r => r.ToString() ?? string.Empty).ToList(),
        };

    /// <summary>
    /// Pure scalar match: does <paramref name="expectedValue"/> appear among <paramref name="actualValues"/>?
    /// IP types (A/AAAA) compare exactly; name and text types compare case-insensitively with trailing
    /// dots already stripped by <see cref="ExtractValues"/>. Returns null on a hit, else a typed error.
    /// </summary>
    internal static string? MatchScalar(string recordType, string expectedValue, IReadOnlyList<string> actualValues)
    {
        var upper = recordType.ToUpperInvariant();
        var expected = upper is "A" or "AAAA" ? expectedValue : expectedValue.TrimEnd('.');
        var comparison = upper is "A" or "AAAA" ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (actualValues.Any(v => v.Equals(expected, comparison)))
            return null;

        return upper switch
        {
            "A" => $"Expected IP {expectedValue} not found in A records.",
            "AAAA" => $"Expected IP {expectedValue} not found in AAAA records.",
            "CNAME" => $"Expected CNAME target {expectedValue} not found.",
            "NS" => $"Expected name server {expectedValue} not found.",
            "PTR" => $"Expected PTR target {expectedValue} not found.",
            "TXT" => $"Expected TXT value \"{expectedValue}\" not found.",
            _ => $"Expected value {expectedValue} not found in {upper} records.",
        };
    }

    /// <summary>
    /// Pure MX match: every expected entry must be present in <paramref name="actual"/>. The exchange
    /// host compares case-insensitively (trailing dots stripped); priority is compared only when the
    /// expectation sets one. Returns null when all entries match, else an error naming the first miss.
    /// </summary>
    internal static string? MatchMxRecords(
        IReadOnlyList<MxExpectation> expected, IReadOnlyList<(string Exchange, int Priority)> actual)
    {
        foreach (var want in expected)
        {
            var wantHost = want.Exchange.TrimEnd('.');
            var hit = actual.Any(a =>
                a.Exchange.TrimEnd('.').Equals(wantHost, StringComparison.OrdinalIgnoreCase)
                && (want.Priority is null || a.Priority == want.Priority));

            if (!hit)
            {
                return want.Priority is null
                    ? $"Expected MX host {want.Exchange} not found."
                    : $"Expected MX host {want.Exchange} with priority {want.Priority} not found.";
            }
        }

        return null;
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

            "CNAME" or "NS" or "PTR" => IsValidHostname(value)
                ? null
                : "must be a valid hostname or FQDN.",

            _ => null // TXT: free text — no format constraint
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

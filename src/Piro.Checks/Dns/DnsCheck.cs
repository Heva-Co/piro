using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using DnsClient;
using DnsClient.Protocol;
using Piro.Checks.Abstractions;
using Piro.Contracts;

namespace Piro.Checks;

/// <summary>
/// Resolves a hostname (optionally across several name servers in parallel) and asserts on the records.
/// Down only when every configured name server failed; a partial failure stays Up and reports the
/// failure count in the "FailedNameServers" dimension so the policy decides severity (RFC 0002). The
/// per-record matching logic is preserved verbatim from the original executor as pure, testable helpers.
/// </summary>
public sealed class DnsCheck : Check<DnsCheckConfig>
{
    public override string CheckId => "DNS";

    /// <summary>How many configured name servers failed to resolve on the last query — more is worse.</summary>
    private static readonly DimensionSpec FailedNameServers = new("FailedNameServers", DimensionComparison.Threshold, ThresholdDirection.HigherIsWorse, "count");

    public override CheckManifest Manifest => new()
    {
        Label = "DNS",
        Description = "Resolve a hostname and assert on the returned records.",
        ConfigType = typeof(DnsCheckConfig),
        Dimensions = [CommonDimensions.Status, CommonDimensions.Latency, FailedNameServers],
    };

    public override async Task<CheckProbeResult> ProbeAsync(DnsCheckConfig config, ICheckHost host, CancellationToken ct = default)
    {
        try
        {
            return await ProbeInternalAsync(config, ct);
        }
        catch (Exception ex)
        {
            return CheckProbeResult.Failed($"Executor error: {ex.Message}");
        }
    }

    private static async Task<CheckProbeResult> ProbeInternalAsync(DnsCheckConfig config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
            return CheckProbeResult.Failed("Host is not configured.");

        var queryType = Enum.TryParse<QueryType>(config.RecordType, ignoreCase: true, out var qt) ? qt : QueryType.A;

        if (!string.IsNullOrWhiteSpace(config.ExpectedValue))
        {
            var validationError = ValidateExpectedValue(config.ExpectedValue, config.RecordType);
            if (validationError is not null)
                return CheckProbeResult.Failed(
                    $"Invalid expected value for record type {config.RecordType}: {validationError}");
        }

        var nameServers = (config.NameServers ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var invalidNs = nameServers.Where(ns => !IsValidNameServer(ns)).ToList();
        if (invalidNs.Count > 0)
            return CheckProbeResult.Failed(
                $"Invalid name server(s): {string.Join(", ", invalidNs)}. Must be a valid IP address or hostname.");

        if (nameServers.Count == 0)
        {
            var single = await QuerySingleAsync(new LookupClient(), config.Host, queryType, config, ct);
            return single.ToProbeResult();
        }

        var tasks = nameServers.Select(ns => QueryNameServerAsync(ns, config.Host, queryType, config, ct)).ToList();
        var results = await Task.WhenAll(tasks);
        return Classify(results, nameServers);
    }

    /// <summary>A single name-server query outcome (internal — not the public probe result).</summary>
    private readonly record struct NsResult(bool Ok, double LatencyMs, string? Error)
    {
        public CheckProbeResult ToProbeResult() => Ok
            ? CheckProbeResult.Ok(Latency(LatencyMs), Failed(0))
            : CheckProbeResult.DownWith(Error ?? "DNS query failed.", Latency(LatencyMs), Failed(1));
    }

    /// <summary>
    /// Aggregates per-name-server results. Down only when every configured server failed; a partial
    /// failure stays Up with the failure count in the FailedNameServers dimension, so severity
    /// (e.g. "alert if 1+ NS fails") is the policy's decision (RFC 0002).
    /// </summary>
    private static CheckProbeResult Classify(NsResult[] results, List<string> nameServers)
    {
        var failures = results.Count(r => !r.Ok);
        var maxLatency = results.Max(r => r.LatencyMs);

        if (failures == 0)
            return CheckProbeResult.Ok(Latency(maxLatency), Failed(0));

        var errorMessage = string.Join("; ", results
            .Select((r, i) => !r.Ok ? $"{nameServers[i]}: {r.Error}" : null)
            .Where(m => m is not null));

        return failures == nameServers.Count
            ? CheckProbeResult.DownWith(errorMessage, Latency(maxLatency), Failed(failures))
            : CheckProbeResult.Ok(Latency(maxLatency), Failed(failures)) with { Message = errorMessage };
    }

    private static CheckDimension Latency(double ms) => CommonDimensions.Latency.Measure(ms);
    private static CheckDimension Failed(int count) => FailedNameServers.Measure(count);

    private static async Task<NsResult> QueryNameServerAsync(
        string nameServer, string host, QueryType queryType, DnsCheckConfig config, CancellationToken ct)
    {
        LookupClient client;
        if (IPAddress.TryParse(nameServer, out var ip))
            client = new LookupClient(ip);
        else
        {
            var resolved = await Dns.GetHostAddressesAsync(nameServer, ct);
            client = resolved.Length > 0 ? new LookupClient(resolved[0]) : new LookupClient();
        }
        return await QuerySingleAsync(client, host, queryType, config, ct);
    }

    private static async Task<NsResult> QuerySingleAsync(
        LookupClient client, string host, QueryType queryType, DnsCheckConfig config, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await client.QueryAsync(host, queryType, cancellationToken: ct);
            sw.Stop();
            var ms = sw.Elapsed.TotalMilliseconds;

            if (result.HasError) return new NsResult(false, ms, result.ErrorMessage);
            if (!result.Answers.Any()) return new NsResult(false, ms, "No DNS records returned.");

            var matchError = CheckExpectedValue(result, config);
            return matchError is not null ? new NsResult(false, ms, matchError) : new NsResult(true, ms, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new NsResult(false, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }

    // ── Pure matching helpers (preserved verbatim from the original DnsCheckExecutor) ──

    private static string? CheckExpectedValue(IDnsQueryResponse result, DnsCheckConfig config)
    {
        var recordType = config.RecordType.ToUpperInvariant();

        if (recordType == "MX")
        {
            var expected = (config.ExpectedMxRecords ?? [])
                .Where(m => !string.IsNullOrWhiteSpace(m.Exchange)).ToList();
            if (expected.Count == 0) return null;
            var actual = result.Answers.OfType<MxRecord>()
                .Select(r => (Exchange: r.Exchange.Value, Priority: (int)r.Preference)).ToList();
            return MatchMxRecords(expected, actual);
        }

        if (string.IsNullOrWhiteSpace(config.ExpectedValue)) return null;
        return MatchScalar(recordType, config.ExpectedValue, ExtractValues(result, recordType));
    }

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

    internal static string? MatchScalar(string recordType, string expectedValue, IReadOnlyList<string> actualValues)
    {
        var upper = recordType.ToUpperInvariant();
        var expected = upper is "A" or "AAAA" ? expectedValue : expectedValue.TrimEnd('.');
        var comparison = upper is "A" or "AAAA" ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (actualValues.Any(v => v.Equals(expected, comparison))) return null;

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
                return want.Priority is null
                    ? $"Expected MX host {want.Exchange} not found."
                    : $"Expected MX host {want.Exchange} with priority {want.Priority} not found.";
        }
        return null;
    }

    private static string? ValidateExpectedValue(string value, string recordType) =>
        recordType.ToUpperInvariant() switch
        {
            "A" => IPAddress.TryParse(value, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork
                ? null : "must be a valid IPv4 address.",
            "AAAA" => IPAddress.TryParse(value, out var ip6) && ip6.AddressFamily == AddressFamily.InterNetworkV6
                ? null : "must be a valid IPv6 address.",
            "CNAME" or "NS" or "PTR" => IsValidHostname(value) ? null : "must be a valid hostname or FQDN.",
            _ => null,
        };

    private static bool IsValidNameServer(string value) => IPAddress.TryParse(value, out _) || IsValidHostname(value);

    private static bool IsValidHostname(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Uri.CheckHostName(value.TrimEnd('.')) != UriHostNameType.Unknown;
    }
}

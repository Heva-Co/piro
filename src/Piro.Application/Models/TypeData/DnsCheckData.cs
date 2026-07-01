namespace Piro.Application.Models.TypeData;

/// <summary>Configuration for a DNS resolution check.</summary>
public record DnsCheckData
{
    public string Host { get; init; } = string.Empty;

    /// <summary>DNS record type to query. Supported: A, AAAA, CNAME.</summary>
    public string RecordType { get; init; } = "A";

    /// <summary>
    /// List of name server IPs or hostnames to query. When empty, uses the system resolver.
    /// Multiple entries are queried in parallel; failures are counted against the thresholds.
    /// </summary>
    public List<string>? NameServers { get; init; }

    /// <summary>
    /// Expected value in the response. Validated by record type:
    /// A → IPv4, AAAA → IPv6, CNAME → hostname.
    /// When null, any successful resolution counts as UP.
    /// </summary>
    public string? ExpectedValue { get; init; }

    /// <summary>Number of name server failures to trigger DEGRADED. Default: 1.</summary>
    public int? DegradedAfter { get; init; }

    /// <summary>Number of name server failures to trigger DOWN. Default: all name servers.</summary>
    public int? DownAfter { get; init; }

    /// <summary>Latency threshold in milliseconds to trigger DEGRADED. Optional.</summary>
    public int? DegradedLatencyMs { get; init; }

    /// <summary>Latency threshold in milliseconds to trigger DOWN. Optional.</summary>
    public int? DownLatencyMs { get; init; }
}

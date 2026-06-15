namespace Piro.Application.Models.TypeData;

/// <summary>Configuration for a DNS resolution check.</summary>
public record DnsCheckData
{
    public string Host { get; init; } = string.Empty;

    /// <summary>Custom name-server IP. When null, uses system resolver.</summary>
    public string? NameServer { get; init; }

    /// <summary>Expected IP address in the response. When null, any successful resolution is UP.</summary>
    public string? ExpectedIp { get; init; }

    /// <summary>DNS record type to query (A, AAAA, CNAME, MX, TXT, …).</summary>
    public string RecordType { get; init; } = "A";
}

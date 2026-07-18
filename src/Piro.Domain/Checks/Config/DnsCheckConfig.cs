using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;

namespace Piro.Domain.Checks.Config;

/// <summary>Configuration for a DNS resolution check.</summary>
public record DnsCheckConfig
{
    [ConfigField("Host", Placeholder = "example.com", HelpText = "The hostname to resolve.")]
    [Required, ConfigValidation("ipOrHostname")]
    public string Host { get; init; } = string.Empty;

    /// <summary>DNS record type to query. Supported: A, AAAA, CNAME.</summary>
    [ConfigField("Record type")]
    [ConfigFieldOptions("A", "AAAA", "CNAME")]
    public string RecordType { get; init; } = "A";

    /// <summary>
    /// List of name server IPs or hostnames to query. When empty, uses the system resolver.
    /// Multiple entries are queried in parallel; failures are counted against the thresholds.
    /// </summary>
    [ConfigField("Name servers", HelpText = "IPs or hostnames to query. Empty = use the system resolver.")]
    public List<string>? NameServers { get; init; }

    /// <summary>
    /// Expected value in the response. Validated by record type:
    /// A → IPv4, AAAA → IPv6, CNAME → hostname.
    /// When null, any successful resolution counts as UP.
    /// </summary>
    [ConfigField("Expected value", HelpText = "Expected record value. Empty = any successful resolution is UP.")]
    [ConfigValidation("dnsExpectedValue")]
    public string? ExpectedValue { get; init; }
}

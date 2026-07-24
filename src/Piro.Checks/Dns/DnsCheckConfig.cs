using System.ComponentModel.DataAnnotations;
using Piro.Contracts;

namespace Piro.Checks;

/// <summary>Configuration for a DNS resolution check.</summary>
public record DnsCheckConfig
{
    [ConfigField("Host", Placeholder = "example.com", HelpText = "The hostname to resolve.")]
    [Required, ConfigValidation("ipOrHostname")]
    public string Host { get; init; } = string.Empty;

    /// <summary>DNS record type to query. Supported: A, AAAA, CNAME, MX, TXT, NS, PTR.</summary>
    [ConfigField("Record type")]
    [ConfigFieldOptions("A", "AAAA", "CNAME", "MX", "TXT", "NS", "PTR")]
    public string RecordType { get; init; } = "A";

    /// <summary>
    /// List of name server IPs or hostnames to query. When empty, uses the system resolver.
    /// Multiple entries are queried in parallel; failures are counted against the thresholds.
    /// </summary>
    [ConfigField("Name servers", HelpText = "IPs or hostnames to query. Empty = use the system resolver.")]
    public List<string>? NameServers { get; init; }

    /// <summary>
    /// Expected value in the response, for record types that carry a single scalar value.
    /// Validated by record type: A → IPv4, AAAA → IPv6, CNAME/NS/PTR → hostname, TXT → free text.
    /// MX uses <see cref="ExpectedMxRecords"/> instead (it carries an exchange host + optional priority),
    /// so this field is hidden when the record type is MX. When empty, any successful resolution counts as UP.
    /// </summary>
    [ConfigField("Expected value", HelpText = "Expected record value. Empty = any successful resolution is UP.")]
    [ConfigValidation("dnsExpectedValue")]
    [VisibleWhen("recordType", "A", "AAAA", "CNAME", "TXT", "NS", "PTR")]
    public string? ExpectedValue { get; init; }

    /// <summary>
    /// Expected MX records. Each entry asserts a mail exchange host and, optionally, its priority.
    /// A check passes only when <em>every</em> configured entry is present in the response — priority
    /// is compared only when set. Empty = any successful MX resolution counts as UP. Only used (and
    /// only shown) when <see cref="RecordType"/> is MX.
    /// </summary>
    [ConfigField("Expected MX records", HelpText = "Each entry must be present in the response. Empty = any successful resolution is UP.")]
    [VisibleWhen("recordType", "MX")]
    public List<MxExpectation>? ExpectedMxRecords { get; init; }
}

/// <summary>A single expected MX record: a mail exchange host and an optional priority.</summary>
public record MxExpectation
{
    /// <summary>The mail exchange host (e.g. <c>mx1.google.com</c>). The trailing dot is optional and ignored.</summary>
    [ConfigField("Exchange (host)", Placeholder = "mx1.example.com", HelpText = "Mail server hostname.")]
    [Required, ConfigValidation("ipOrHostname")]
    public string Exchange { get; init; } = string.Empty;

    /// <summary>Optional MX priority (preference). When set, both host and priority must match; when empty, only the host is compared.</summary>
    [ConfigField("Priority", Placeholder = "10", HelpText = "Optional. When set, priority must match too.")]
    public int? Priority { get; init; }
}

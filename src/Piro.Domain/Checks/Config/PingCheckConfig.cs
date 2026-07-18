using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Piro.Domain.Attributes;

namespace Piro.Domain.Checks.Config;

/// <summary>Configuration for an ICMP ping check.</summary>
public record PingCheckConfig
{
    [ConfigField("Host", Placeholder = "example.com", HelpText = "The host to ping.")]
    [Required, ConfigValidation("ipOrHostname")]
    public string Host { get; init; } = string.Empty;

    [ConfigField("Timeout (ms)", HelpText = "Abort the ping after this many milliseconds. Must be shorter than the check interval.")]
    [JsonPropertyName("timeout")]
    public int TimeoutMs { get; init; } = 5000;
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Piro.Contracts;

namespace Piro.Checks;

/// <summary>Configuration for a TCP port connectivity check.</summary>
public record TcpCheckConfig
{
    [ConfigField("Host", Placeholder = "example.com", HelpText = "The host to connect to.")]
    [Required, ConfigValidation("ipOrHostname")]
    public string Host { get; init; } = string.Empty;

    [ConfigField("Port")]
    [Required, ConfigValidation("port")]
    public int Port { get; init; }

    [ConfigField("Timeout (ms)", HelpText = "Abort the connection after this many milliseconds. Must be shorter than the check interval.")]
    [JsonPropertyName("timeout")]
    public int TimeoutMs { get; init; } = 5000;
}

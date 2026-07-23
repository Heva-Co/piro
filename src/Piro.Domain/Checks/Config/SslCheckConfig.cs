using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;
using Piro.Contracts;

namespace Piro.Domain.Checks.Config;

/// <summary>Configuration for an SSL certificate check.</summary>
public record SslCheckConfig
{
    [ConfigField("Host", Placeholder = "example.com", HelpText = "The host whose TLS certificate to check.")]
    [Required, ConfigValidation("ipOrHostname")]
    public string Host { get; init; } = string.Empty;

    [ConfigField("Port", HelpText = "TLS port. Usually 443.")]
    [ConfigValidation("port")]
    public int Port { get; init; } = 443;
}

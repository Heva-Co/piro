using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Piro.Domain.Attributes;

namespace Piro.Domain.Checks.Config;

/// <summary>Configuration for a gRPC health-check probe.</summary>
public record GrpcCheckConfig
{
    [ConfigField("Host", Placeholder = "example.com", HelpText = "The gRPC server host to connect to.")]
    [Required, ConfigValidation("ipOrHostname")]
    public string Host { get; init; } = string.Empty;

    [ConfigField("Port")]
    [Required, ConfigValidation("port")]
    public int Port { get; init; }

    [ConfigField("Service", HelpText = "The service name to health-check (grpc.health.v1.Health/Check). Empty checks overall server health.")]
    public string? Service { get; init; }

    [ConfigField("Use TLS", HelpText = "Connect over TLS (https). Leave off for a plaintext (h2c) server.")]
    public bool Tls { get; init; } = true;

    [ConfigField("Timeout (ms)", HelpText = "Abort the probe after this many milliseconds. Must be shorter than the check interval.")]
    [JsonPropertyName("timeout")]
    public int TimeoutMs { get; init; } = 5000;
}

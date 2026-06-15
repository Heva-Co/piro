using System.Text.Json.Serialization;

namespace Piro.Application.Models.TypeData;

/// <summary>Configuration for a TCP port connectivity check.</summary>
public record TcpCheckData
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }

    [JsonPropertyName("timeout")]
    public int TimeoutMs { get; init; } = 5000;
}

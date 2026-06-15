using System.Text.Json.Serialization;

namespace Piro.Application.Models.TypeData;

/// <summary>Configuration for an ICMP ping check.</summary>
public record PingCheckData
{
    public string Host { get; init; } = string.Empty;

    [JsonPropertyName("timeout")]
    public int TimeoutMs { get; init; } = 5000;
}

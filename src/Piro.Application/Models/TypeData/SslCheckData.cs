namespace Piro.Application.Models.TypeData;

/// <summary>Configuration for an SSL certificate check.</summary>
public record SslCheckData
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 443;
}

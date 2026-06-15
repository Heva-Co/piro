namespace Piro.Application.Models.TypeData;

/// <summary>Configuration for an SSL certificate check.</summary>
public record SslCheckData
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 443;

    /// <summary>Certificate expiry within this many days is reported as DEGRADED.</summary>
    public int WarningDaysBeforeExpiry { get; init; } = 30;
}

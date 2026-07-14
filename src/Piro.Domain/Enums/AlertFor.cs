namespace Piro.Domain.Enums;

/// <summary>Metric that an alert configuration monitors.</summary>
public enum AlertFor
{
    Status,
    Latency,

    /// <summary>Days remaining until a SSL certificate expires. Compares against <see cref="Entities.CheckDataPoint.MetricValue"/>.</summary>
    CertExpiry,

    /// <summary>Number of DNS name servers that failed to resolve on the last query. Compares against <see cref="Entities.CheckDataPoint.MetricValue"/>.</summary>
    FailedNameServers
}

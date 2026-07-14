using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>Minute-aligned time-series data point for a single check execution result.</summary>
public class CheckDataPoint
{
    public int CheckId { get; set; }

    /// <summary>Unix timestamp in seconds, aligned to the start of the minute.</summary>
    public long Timestamp { get; set; }

    public ServiceStatus Status { get; set; }
    public double? LatencyMs { get; set; }

    /// <summary>
    /// Generic raw metric slot for executors whose severity-relevant number isn't latency —
    /// e.g. days until SSL certificate expiry, number of DNS name servers that failed to
    /// resolve, or hours since a GCP Cloud Run Job's last successful execution.
    /// <see cref="AlertFor.CertExpiry"/>, <see cref="AlertFor.FailedNameServers"/>, and similar
    /// non-latency <c>AlertFor</c> values compare <see cref="AlertConfig.AlertValue"/>
    /// against this field the same way <c>AlertFor.Latency</c> compares against <see cref="LatencyMs"/>.
    /// Null for check types with no single comparable raw number (e.g. Ping, TCP) or where
    /// <see cref="LatencyMs"/> already is that number.
    /// </summary>
    public double? MetricValue { get; set; }

    /// <summary>Origin of this data point. See <see cref="DataPointType"/> for possible values.</summary>
    public DataPointType? DataType { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Region label of the worker that produced this data point.
    /// <c>"default"</c> for the embedded local worker; the worker's configured region for remote workers.
    /// Part of the primary key — enables multiple data points per minute when a check is multi-region.
    /// </summary>
    public string WorkerRegion { get; set; } = "default";

    public Check Check { get; set; } = null!;
}

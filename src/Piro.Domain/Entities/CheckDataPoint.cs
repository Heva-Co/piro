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

    /// <summary>Origin of this data point: REALTIME, MAINTENANCE, INCIDENT, or DEFAULT.</summary>
    public string? DataType { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Region label of the worker that produced this data point.
    /// <c>"default"</c> for the embedded local worker; the worker's configured region for remote workers.
    /// Part of the primary key — enables multiple data points per minute when a check is multi-region.
    /// </summary>
    public string WorkerRegion { get; set; } = "default";

    public Check Check { get; set; } = null!;
}

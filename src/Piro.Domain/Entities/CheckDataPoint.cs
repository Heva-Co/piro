using System.ComponentModel.DataAnnotations.Schema;
using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>Minute-aligned time-series data point for a single check execution result.</summary>
public class CheckDataPoint
{
    public int CheckId { get; set; }

    /// <summary>Unix timestamp in seconds, aligned to the start of the minute.</summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// The availability outcome of the execution. Kept as its own column (not a dimension) because it is
    /// the axis every uptime/status-page aggregation groups by, and an alert rule with an Equality
    /// comparison matches against it.
    /// </summary>
    public ServiceStatus Status { get; set; }

    /// <summary>
    /// Every numeric measurement the check reported, keyed by dimension name (e.g. "Latency",
    /// "CertExpiry", "FailedNameServers", "LastRunAge", "FailedTasks"). Stored as a JSON column so a
    /// check can persist several simultaneous metrics without a schema change per check. An alert rule
    /// on a numeric dimension looks its value up here by name; the comparison direction/unit come from
    /// the check's declared <c>DimensionSpec</c>, not from this row. Empty for checks that report no
    /// numeric metric on a given execution.
    /// </summary>
    public Dictionary<string, double> Dimensions { get; set; } = [];

    /// <summary>
    /// Round-trip latency in milliseconds, read from the <see cref="Dimensions"/> "Latency" entry.
    /// Convenience accessor for the many in-memory callers (DTOs, latest-point reads) that predate the
    /// dimension model; SQL aggregations over latency query the JSON column directly.
    /// </summary>
    [NotMapped]
    public double? LatencyMs => Dimensions.TryGetValue("Latency", out var v) ? v : null;

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

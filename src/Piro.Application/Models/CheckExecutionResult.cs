using Piro.Domain.Enums;

namespace Piro.Application.Models;

/// <summary>
/// Result produced by a single check execution. Carries the availability <see cref="Status"/> plus every
/// numeric measurement the check reported, keyed by dimension name (e.g. "Latency", "CertExpiry"). A
/// check itself only returns a raw Up/Down/Error outcome and its dimensions; mapping that to a
/// <see cref="ServiceStatus"/> happens at the execution boundary (the registry adapter). The same result
/// shape is produced whether the check ran in-process or on a remote worker.
/// </summary>
public record CheckExecutionResult(
    ServiceStatus Status,
    IReadOnlyDictionary<string, double> Dimensions,
    string? ErrorMessage)
{
    private static readonly IReadOnlyDictionary<string, double> Empty = new Dictionary<string, double>();

    /// <summary>Convenience for callers that only need latency (charts, aggregates).</summary>
    public double? LatencyMs => Dimensions.TryGetValue("Latency", out var v) ? v : null;

    /// <summary>A result with no measured dimensions (an executor error, a gap point, a bare status).</summary>
    public static CheckExecutionResult Of(ServiceStatus status, string? error = null) =>
        new(status, Empty, error);

    /// <summary>A result carrying only a latency measurement (a multi-region aggregate).</summary>
    public static CheckExecutionResult WithLatency(ServiceStatus status, double? latencyMs, string? error = null) =>
        latencyMs.HasValue
            ? new(status, new Dictionary<string, double> { ["Latency"] = latencyMs.Value }, error)
            : Of(status, error);
}

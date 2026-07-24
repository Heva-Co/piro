using Piro.Domain.Enums;

namespace Piro.Application.Models.Worker;

// ── API → Worker ─────────────────────────────────────────────────────────────

/// <summary>Sent by the API once on successful connection to acknowledge the worker.</summary>
public record WorkerAckMessage(Guid WorkerId, string Region);

/// <summary>Instructs the worker to run a check and return the result.</summary>
/// <param name="BatchId">
/// Non-null for multi-region checks — groups all worker results belonging to the same
/// Quartz execution cycle so the API can aggregate them before updating <c>CurrentStatus</c>.
/// Null for single-region (local) checks.
/// </param>
public record WorkerExecuteMessage(
    string JobId,
    int CheckId,
    CheckType CheckType,
    string TypeDataJson,
    string? BatchId = null);

// ── Worker → API ─────────────────────────────────────────────────────────────

/// <summary>Sent by the worker every minute to signal liveness and report its version.</summary>
public record WorkerHeartbeatMessage(string Version);

/// <summary>Sent by the worker after completing a check execution.</summary>
/// <param name="BatchId">Echo of <see cref="WorkerExecuteMessage.BatchId"/>. Null for single-region checks.</param>
/// <param name="Status">String representation of <see cref="ServiceStatus"/> to avoid enum serialization issues over SignalR.</param>
/// <param name="Dimensions">
/// Every numeric measurement the check reported, keyed by dimension name (e.g. "Latency", "CertExpiry").
/// A remote worker runs the same checks as the in-process worker, so it returns the full set of
/// dimensions, not just latency — otherwise multi-region alerts on non-latency dimensions could not fire.
/// </param>
public record WorkerResultMessage(
    string JobId,
    int CheckId,
    string Status,
    IReadOnlyDictionary<string, double> Dimensions,
    string? ErrorMessage,
    DateTime ExecutedAt,
    string? BatchId = null);

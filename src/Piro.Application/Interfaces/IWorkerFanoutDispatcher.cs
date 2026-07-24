using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>
/// The fan-out operations <see cref="ICheckJobDispatcher"/> routing uses to place a check on workers
/// (RFC 0008 Part B): dispatch to a set of eligible workers, or record the two non-run outcomes. Extracted
/// as an interface so the routing decision (no-tags vs tag-matched vs outage vs unschedulable) is unit
/// testable without a live SignalR hub.
/// </summary>
public interface IWorkerFanoutDispatcher
{
    /// <summary>Dispatches the check to each given worker by its transport (in-process or SignalR). Empty set records MONITOR_OUTAGE.</summary>
    Task DispatchToWorkersAsync(Check check, IReadOnlyList<WorkerInfo> workers, CancellationToken ct = default);

    /// <summary>Records a transient MONITOR_OUTAGE: a matching worker is registered but not currently connected (§4.6).</summary>
    Task RecordMonitorOutageAsync(Check check, string message, CancellationToken ct = default);

    /// <summary>Records an UNSCHEDULABLE config error: no registered worker can ever match the required tags (§4.6).</summary>
    Task RecordUnschedulableAsync(Check check, CancellationToken ct = default);
}

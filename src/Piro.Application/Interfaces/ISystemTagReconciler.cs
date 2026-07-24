using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>
/// Materializes the stored <c>piro:*</c> system tags for an entity from the facts on its own row (RFC 0008
/// §4.2), write-through: called from the same app-service method that changes a source field, so the tag
/// rows stay in sync without a periodic sweep. A single shared component every write path calls, so a new
/// writer added later cannot silently bypass reconciliation (§8).
/// </summary>
public interface ISystemTagReconciler
{
    /// <summary>Reconciles <c>piro:check-type</c> and <c>piro:multi-region</c> from the check's fields.</summary>
    Task ReconcileCheckAsync(Check check, CancellationToken ct = default);

    /// <summary>Reconciles <c>piro:region</c>, <c>piro:builtin</c>, and <c>piro:default</c> from the worker's fields.</summary>
    Task ReconcileWorkerAsync(WorkerRegistration worker, CancellationToken ct = default);
}

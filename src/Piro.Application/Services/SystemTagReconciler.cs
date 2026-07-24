using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Application.Services;

/// <summary>
/// Write-through reconciliation of the stored <c>piro:*</c> system tags (RFC 0008 §4.2). Boolean facts map
/// to a key-only tag whose presence is the flag (added when true, removed when false); valued facts map to
/// a tag whose value tracks the field.
/// </summary>
public class SystemTagReconciler(ITagRepository tags) : ISystemTagReconciler
{
    public async Task ReconcileCheckAsync(Check check, CancellationToken ct = default)
    {
        // piro:check-type: valued, mirrors Check.Type (lower-cased to match tag-value conventions).
        await tags.SetCheckSystemTagAsync(check.Id, "piro:check-type", check.Type.ToString().ToLowerInvariant(), ct);
    }

    public async Task ReconcileWorkerAsync(WorkerRegistration worker, CancellationToken ct = default)
    {
        // piro:region: valued, mirrors WorkerRegistration.Region.
        await tags.SetWorkerSystemTagAsync(worker.Id, "piro:region", worker.Region, ct);

        // piro:builtin / piro:default: key-only flags.
        await SetFlagAsync(worker.IsBuiltIn,
            () => tags.SetWorkerSystemTagAsync(worker.Id, "piro:builtin", null, ct),
            () => tags.RemoveWorkerSystemTagAsync(worker.Id, "piro:builtin", ct));
        await SetFlagAsync(worker.IsDefault,
            () => tags.SetWorkerSystemTagAsync(worker.Id, "piro:default", null, ct),
            () => tags.RemoveWorkerSystemTagAsync(worker.Id, "piro:default", ct));
    }

    private static async Task SetFlagAsync(bool present, Func<Task> add, Func<Task> remove)
    {
        if (present) await add();
        else await remove();
    }
}

using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Tags;

namespace Piro.Infrastructure.Workers;

/// <summary>
/// Routes each check to the workers that should run it (RFC 0008 Part B). Scheduling is purely tag-based:
/// <list type="bullet">
///   <item>No required worker tags ⇒ the check runs on every live worker (the built-in in-process worker
///   plus any connected remote workers). In a single-node deployment that is just the built-in.</item>
///   <item>Required worker tags ⇒ the check runs only on live workers whose tags match (flat intersection,
///   §4.5). Single-region is expressed by requiring a worker tag such as <c>piro:default</c>.</item>
/// </list>
/// Each eligible worker is dispatched by its own transport (in-process vs SignalR) inside
/// <see cref="RemoteCheckJobDispatcher.DispatchToWorkersAsync"/>, so there is no synthetic-connection
/// special case here. When workers exist but none match, that is distinguished as MONITOR_OUTAGE (a
/// matching worker is registered but offline) vs UNSCHEDULABLE (no registered worker can ever match), §4.6.
/// </summary>
internal class RoutingCheckJobDispatcher(
    IWorkerFanoutDispatcher remote,
    IWorkerRegistry registry,
    ITagRepository tags) : ICheckJobDispatcher
{
    public async Task DispatchAsync(Check check, CancellationToken ct = default)
    {
        var required = await tags.GetRequiredWorkerTagsAsync(check.Id, ct);

        // No required tags ⇒ run on every live worker (built-in + remotes). DispatchToWorkersAsync routes
        // each by transport and records MONITOR_OUTAGE if the set is empty, so routing stays total.
        if (required.Count == 0)
        {
            await remote.DispatchToWorkersAsync(check, registry.GetAll(), ct);
            return;
        }

        var requirement = required
            .Select(rt => new RequiredWorkerTag(rt.Tag.Key, rt.Value))
            .ToArray();
        var eligible = registry.GetAll()
            .Where(w => WorkerTagMatcher.IsEligible(requirement, w.Tags))
            .ToList();

        if (eligible.Count > 0)
        {
            await remote.DispatchToWorkersAsync(check, eligible, ct);
            return;
        }

        // No live worker matches. Distinguish the two empty-match causes against the REGISTERED workers:
        // a registered-but-offline match is a transient MONITOR_OUTAGE; no registered worker able to match
        // is a permanent UNSCHEDULABLE config error (§4.6).
        var registeredMatches = (await tags.GetAllWorkerTagSetsAsync(ct))
            .Any(wt => WorkerTagMatcher.IsEligible(requirement, wt));

        if (registeredMatches)
            await remote.RecordMonitorOutageAsync(check, "A worker matching the required tags exists but is not connected", ct);
        else
            await remote.RecordUnschedulableAsync(check, ct);
    }
}

using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Tags;

namespace Piro.Infrastructure.Workers;

/// <summary>
/// Routes each check to the appropriate dispatcher. When a check declares no required worker tags (RFC 0008
/// Part B), routing is the classic <see cref="Check.IsMultiRegion"/> decision:
/// <list type="bullet">
///   <item><see langword="false"/> — <see cref="LocalCheckJobDispatcher"/>: in-process, embedded worker.</item>
///   <item><see langword="true"/>  — <see cref="RemoteCheckJobDispatcher"/>: fan-out to all connected workers.</item>
/// </list>
/// When a check DOES declare required worker tags, it is tag-scheduled: routing computes the eligible worker
/// subset (a flat tag intersection, §4.5) and fans out to just those workers. An empty required set means
/// "run anywhere", so tagging is opt-in and the default path is unchanged.
/// </summary>
internal class RoutingCheckJobDispatcher(
    LocalCheckJobDispatcher local,
    RemoteCheckJobDispatcher remote,
    IWorkerRegistry registry,
    ITagRepository tags) : ICheckJobDispatcher
{
    public async Task DispatchAsync(Check check, CancellationToken ct = default)
    {
        var required = await tags.GetRequiredWorkerTagsAsync(check.Id, ct);

        // No required worker tags ⇒ today's behavior: IsMultiRegion decides local vs. fan-out.
        if (required.Count == 0)
        {
            await DispatchByRegionAsync(check, ct);
            return;
        }

        // Tag-scheduled: run only on workers whose tags match the check's requirement (§4.5). This
        // overrides the local/remote choice because the eligible workers may be remote.
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

        // No LIVE worker matches. Distinguish the two empty-match causes (§4.6) against the set of
        // REGISTERED workers (not just connected ones):
        //   - a registered worker matches but none is currently live  ⇒ MONITOR_OUTAGE (transient; the
        //     matching worker is down/disconnected and will heal when it returns).
        //   - no registered worker can ever match the required tags    ⇒ UNSCHEDULABLE (a config error the
        //     operator must fix by editing the check).
        var registeredMatches = (await tags.GetAllWorkerTagSetsAsync(ct))
            .Any(wt => WorkerTagMatcher.IsEligible(requirement, wt));

        if (registeredMatches)
            await remote.RecordMonitorOutageAsync(check, "A worker matching the required tags exists but is not connected", ct);
        else
            await remote.RecordUnschedulableAsync(check, ct);
    }

    private Task DispatchByRegionAsync(Check check, CancellationToken ct)
    {
        // Built-in API worker is active when it has a live registry entry.
        var apiIsWorker = registry.GetByConnectionId(ApiWorkerHostedService.ApiWorkerConnectionId) is not null;
        if (apiIsWorker)
            return check.IsMultiRegion ? remote.DispatchAsync(check, ct) : local.DispatchAsync(check, ct);
        return check.IsMultiRegion ? remote.DispatchAsync(check, ct) : remote.DispatchToDefaultWorkerAsync(check, ct);
    }
}

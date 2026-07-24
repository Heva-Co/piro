using Piro.Application.Interfaces;
using Piro.Checks.Abstractions;

namespace Piro.Infrastructure.Checks;

/// <summary>
/// Scoped, mutable holder for the check currently executing in this DI scope. <see cref="RegistryCheckExecutor"/>
/// sets it before invoking a probe that declares <see cref="CheckManifest.ConsumesCheckPoints"/>, and
/// <see cref="OwnCheckPoints"/> reads it — so the reader is bound to exactly one check without the probe
/// ever learning the id. One instance per execution scope (checks run concurrently on separate scopes),
/// so there is no cross-check leakage.
/// </summary>
internal sealed class CurrentCheckContext
{
    public int? CheckId { get; set; }
}

/// <summary>
/// Concrete <see cref="IOwnCheckPoints"/> (RFC 0013): returns only the data points of the check the
/// current scope is running (from <see cref="CurrentCheckContext"/>), never any other check's. Resolved
/// by a probe through the allow-listed <see cref="ICheckHost"/>; the probe passes no id and cannot widen
/// the query beyond its own check.
/// </summary>
internal sealed class OwnCheckPoints(CurrentCheckContext current, ICheckDataPointRepository dataPoints) : IOwnCheckPoints
{
    public async Task<CheckPoint?> LatestAsync(CancellationToken ct = default)
    {
        var checkId = Require();
        var latest = await dataPoints.GetLatestByCheckIdAsync(checkId, ct);
        return latest is null ? null : ToPoint(latest);
    }

    public async Task<IReadOnlyList<CheckPoint>> RecentAsync(int limit, CancellationToken ct = default)
    {
        var checkId = Require();
        var rows = await dataPoints.GetByCheckIdAsync(checkId, limit: limit, ct: ct);
        return rows.Select(ToPoint).ToList();
    }

    private int Require() =>
        current.CheckId ?? throw new InvalidOperationException(
            "IOwnCheckPoints was resolved outside a check execution scope — no current check is set.");

    private static CheckPoint ToPoint(Domain.Entities.CheckDataPoint p) =>
        new(p.Timestamp, p.Status.ToString(), p.Dimensions);
}

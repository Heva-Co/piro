using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Checks;

/// <summary>
/// <c>piro:has-incident</c>: services with an open (unresolved) incident linkage (RFC 0008 §4.2).
/// </summary>
internal class HasIncidentComputedTag(PiroDbContext db) : IComputedSystemTagBatch<Service>
{
    public string Key => "piro:has-incident";

    public async Task<ISet<int>> ComputeForAsync(IReadOnlyCollection<int> entityIds, CancellationToken ct)
    {
        var ids = await db.IncidentServices
            .Where(link => entityIds.Contains(link.ServiceId)
                && link.Incident.Status != IncidentStatus.Resolved
                && link.Incident.Status != IncidentStatus.Merged)
            .Select(link => link.ServiceId)
            .Distinct()
            .ToListAsync(ct);
        return ids.ToHashSet();
    }
}

/// <summary>
/// <c>piro:has-alerts</c>: services with an active (unresolved) alert (RFC 0008 §4.2).
/// </summary>
internal class HasAlertsComputedTag(PiroDbContext db) : IComputedSystemTagBatch<Service>
{
    public string Key => "piro:has-alerts";

    public async Task<ISet<int>> ComputeForAsync(IReadOnlyCollection<int> entityIds, CancellationToken ct)
    {
        var ids = await db.Alerts
            .Where(a => a.ServiceId != null
                && entityIds.Contains(a.ServiceId.Value)
                && a.ResolvedAt == null)
            .Select(a => a.ServiceId!.Value)
            .Distinct()
            .ToListAsync(ct);
        return ids.ToHashSet();
    }
}

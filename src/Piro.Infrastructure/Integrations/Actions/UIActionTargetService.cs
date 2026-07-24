using Piro.Application.Interfaces;
using Piro.Contracts;

namespace Piro.Infrastructure.Integrations.Actions;

/// <summary>
/// Resolves the neutral <see cref="ActionTarget"/> for an Alert/Incident/Maintenance (RFC 0016,
/// "Forma 1"). The executor calls this to build the <see cref="UIActionContext"/> it hands a UI action,
/// so the action gets its target already resolved and never loads a Piro entity itself. This is a
/// Piro-internal seam (Application interface, Infra impl) — not part of the integration contract.
/// </summary>
internal sealed class UIActionTargetService(
    IAlertRepository alertRepo,
    IIncidentRepository incidentRepo,
    IMaintenanceRepository maintenanceRepo) : IUIActionTargetService
{
    public async Task<ActionTarget?> GetTargetAsync(UISurface context, int targetId, CancellationToken ct = default)
    {
        switch (context)
        {
            case UISurface.Alert:
                var alert = await alertRepo.GetByIdAsync(targetId, ct);
                if (alert is null) return null;
                var subject = alert.Service?.Name ?? alert.Check?.Name ?? "Alert";
                return new ActionTarget(
                    context, alert.Id,
                    Title: $"[Piro] {subject} — alert #{alert.Id}",
                    Summary: alert.Message ?? $"Alert #{alert.Id} fired at {alert.FiredAt:u}.",
                    PiroUrl: $"/alerts/{alert.Id}");

            case UISurface.Incident:
                var incident = await incidentRepo.GetByIdAsync(targetId, ct);
                if (incident is null) return null;
                return new ActionTarget(
                    context, incident.Id,
                    Title: $"[Piro] {incident.Title}",
                    Summary: incident.Title,
                    PiroUrl: $"/incidents/{incident.Id}");

            case UISurface.Maintenance:
                var maintenance = await maintenanceRepo.GetByIdAsync(targetId, ct);
                if (maintenance is null) return null;
                return new ActionTarget(
                    context, maintenance.Id,
                    Title: $"[Piro] {maintenance.Title}",
                    Summary: maintenance.Description ?? maintenance.Title,
                    PiroUrl: $"/maintenances/{maintenance.Id}");

            default:
                return null;
        }
    }
}

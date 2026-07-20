using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Piro.Application.Integrations.Actions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Integrations.OAuth;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Integrations.Actions;

/// <summary>
/// The concrete <see cref="IActionHost"/> — the one place an integration action's needs are translated
/// into real persistence and OAuth access (RFC 0012). It reads targets through the existing repositories,
/// persists external links to the <c>ExternalReferences</c> table, and hands out OAuth bearer tokens via
/// RFC 0004's provider. Actions depend only on the <see cref="IActionHost"/> interface, so this class is
/// the sole DB/OAuth coupling for the whole action layer — the seam a future plugin host would reuse.
/// </summary>
internal sealed class ActionHost(
    PiroDbContext db,
    IAlertRepository alertRepo,
    IIncidentRepository incidentRepo,
    IMaintenanceRepository maintenanceRepo,
    IIntegrationRepository integrationRepo,
    IOAuthTokenStore tokenStore,
    IOAuthTokenProvider tokenProvider) : IActionHost
{
    public async Task<ActionTarget?> GetTargetAsync(ActionContext context, int targetId, CancellationToken ct = default)
    {
        switch (context)
        {
            case ActionContext.Alert:
                var alert = await alertRepo.GetByIdAsync(targetId, ct);
                if (alert is null) return null;
                var subject = alert.Service?.Name ?? alert.Check?.Name ?? "Alert";
                return new ActionTarget(
                    context, alert.Id,
                    Title: $"[Piro] {subject} — alert #{alert.Id}",
                    Summary: alert.Message ?? $"Alert #{alert.Id} fired at {alert.FiredAt:u}.",
                    PiroUrl: $"/alerts/{alert.Id}");

            case ActionContext.Incident:
                var incident = await incidentRepo.GetByIdAsync(targetId, ct);
                if (incident is null) return null;
                return new ActionTarget(
                    context, incident.Id,
                    Title: $"[Piro] {incident.Title}",
                    Summary: incident.Title,
                    PiroUrl: $"/incidents/{incident.Id}");

            case ActionContext.Maintenance:
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

    public async Task LinkExternalAsync(ExternalReferenceRequest request, CancellationToken ct = default)
    {
        var reference = new ExternalReference
        {
            TargetType = request.Context,
            TargetId = request.TargetId,
            IntegrationId = request.IntegrationId,
            ActionId = request.ActionId,
            ExternalId = request.ExternalId,
            Url = request.Url,
            Label = request.Label,
            MetadataJson = SerializeMetadata(request.Metadata),
        };

        db.ExternalReferences.Add(reference);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ExternalReferenceView>> GetLinksAsync(ActionContext context, int targetId, CancellationToken ct = default)
    {
        var rows = await db.ExternalReferences
            .AsNoTracking()
            .Where(r => r.TargetType == context && r.TargetId == targetId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(ToView).ToList();
    }

    public Task<string> GetBearerTokenAsync(Guid integrationId, CancellationToken ct = default) =>
        tokenProvider.GetAccessTokenAsync(integrationId, ct);

    public async Task<bool> IsOAuthConnectedAsync(Guid integrationId, CancellationToken ct = default) =>
        await tokenStore.GetAsync(integrationId, ct) is not null;

    public async Task<string?> GetConfigValueAsync(Guid integrationId, string key, CancellationToken ct = default)
    {
        var integration = await integrationRepo.GetByIdAsync(integrationId, ct);
        if (integration is null || string.IsNullOrWhiteSpace(integration.ConfigJson))
            return null;

        // Read straight from stored ConfigJson: only non-secret keys are read through here, and
        // secret values are stored encrypted, so they'd come back as ciphertext (never usable) anyway.
        using var doc = JsonDocument.Parse(integration.ConfigJson);
        return doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static ExternalReferenceView ToView(ExternalReference r) =>
        new(r.TargetType, r.TargetId, r.IntegrationId, r.ActionId, r.ExternalId, r.Url, r.Label, DeserializeMetadata(r.MetadataJson));

    private static string SerializeMetadata(IReadOnlyDictionary<string, object?>? metadata) =>
        metadata is null || metadata.Count == 0 ? "{}" : JsonUtils.Serialize(metadata);

    private static IReadOnlyDictionary<string, object?>? DeserializeMetadata(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return null;
        return JsonUtils.Deserialize<Dictionary<string, object?>>(json);
    }
}

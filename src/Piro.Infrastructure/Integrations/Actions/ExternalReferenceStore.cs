using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Contracts;
using Piro.Domain.Entities;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Integrations.Actions;

/// <summary>
/// The concrete <see cref="IExternalReferenceStore"/> (RFC 0012 §4.5): persists the outbound link a UI
/// action produced ("this Alert ↔ that Jira ticket") and reads existing links for gating/dedup and the
/// UI. A Piro-internal seam the executor uses — an integration never touches this; it returns a
/// <see cref="UIActionResult"/> and the executor persists it here.
/// </summary>
internal sealed class ExternalReferenceStore(PiroDbContext db) : IExternalReferenceStore
{
    public async Task LinkAsync(ExternalReferenceRequest request, CancellationToken ct = default)
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

    public async Task<IReadOnlyList<ExternalReferenceView>> GetLinksAsync(UISurface context, int targetId, CancellationToken ct = default)
    {
        var rows = await db.ExternalReferences
            .AsNoTracking()
            .Where(r => r.TargetType == context && r.TargetId == targetId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(ToView).ToList();
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

using Piro.Contracts;

namespace Piro.Application.Interfaces;

/// <summary>
/// Resolves the neutral <see cref="ActionTarget"/> for a local object (Alert/Incident/Maintenance) the
/// executor is about to run a UI action against (RFC 0016, "Forma 1"). This is a Piro-internal seam,
/// <b>not</b> part of the integration contract: the executor calls it to build the
/// <see cref="UIActionContext"/> it hands to the action, so the action receives its target already
/// resolved and never asks Piro to load an entity for it.
/// </summary>
public interface IUIActionTargetService
{
    /// <summary>Builds the neutral target view for a surface + id, or null if the object doesn't exist.</summary>
    Task<ActionTarget?> GetTargetAsync(UISurface context, int targetId, CancellationToken ct = default);
}

/// <summary>
/// The outbound external-reference store (RFC 0012 §4.5): where the executor persists the link a UI
/// action produced ("this Alert ↔ that Jira ticket") and reads existing links for gating/dedup and the
/// UI. A Piro-internal seam used by the executor/AppService — an integration never sees it; it just
/// returns a <see cref="UIActionResult"/> and the caller persists it here.
/// </summary>
public interface IExternalReferenceStore
{
    /// <summary>Attaches an outbound external reference to a local object.</summary>
    Task LinkAsync(ExternalReferenceRequest request, CancellationToken ct = default);

    /// <summary>Lists external references already attached to a target (gating, dedup, display).</summary>
    Task<IReadOnlyList<ExternalReferenceView>> GetLinksAsync(UISurface context, int targetId, CancellationToken ct = default);
}

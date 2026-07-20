using Piro.Domain.Enums;

namespace Piro.Application.Integrations.Actions;

/// <summary>
/// The single, stable seam between an <see cref="IIntegrationAction"/> and the rest of Piro (RFC 0012).
/// An action never touches a repository, <c>DbContext</c>, or the OAuth token store directly — it asks
/// the host. This is the "internal SDK" a future hot-loaded plugin would consume unchanged: external
/// code links things to Piro entities and reads tokens <b>through the Piro API surface</b>, not against
/// its persistence.
/// <para>
/// Everything here is scoped to what an action legitimately needs: read the target neutrally, attach an
/// outbound link, list existing links (for gating/dedup), and obtain a fresh OAuth bearer token.
/// </para>
/// </summary>
public interface IActionHost
{
    /// <summary>
    /// Loads the target object as a neutral, read-only <see cref="ActionTarget"/>. Returns null if the
    /// object doesn't exist. The host switches on <paramref name="context"/> internally over the real
    /// repositories; the action stays ignorant of entity shapes.
    /// </summary>
    Task<ActionTarget?> GetTargetAsync(ActionContext context, int targetId, CancellationToken ct = default);

    /// <summary>
    /// Attaches an outbound external reference to a local object. The action supplies what it created;
    /// the host persists the <c>ExternalReference</c> row. The action never knows the table exists.
    /// </summary>
    Task LinkExternalAsync(ExternalReferenceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Lists external references already attached to a target — used to gate actions (e.g. "comment"
    /// requires an existing ticket) and to surface/dedup links in the UI.
    /// </summary>
    Task<IReadOnlyList<ExternalReferenceView>> GetLinksAsync(ActionContext context, int targetId, CancellationToken ct = default);

    /// <summary>
    /// Returns a currently-valid OAuth access token for the integration, refreshing transparently
    /// (wraps RFC 0004's token provider). Throws if the integration is not OAuth-connected.
    /// </summary>
    Task<string> GetBearerTokenAsync(Guid integrationId, CancellationToken ct = default);
}

using Piro.Domain.Enums;

namespace Piro.Application.Integrations.Actions;

/// <summary>
/// A declared integration action: its manifest metadata joined to the handler that implements it
/// (RFC 0012). Discovery, the executor, and the frontend read <see cref="Descriptor"/>; only the
/// executor touches <see cref="Handler"/>.
/// </summary>
public sealed record RegisteredAction(ActionDescriptor Descriptor, IIntegrationAction Handler);

/// <summary>
/// The metadata half of an action — everything declared on the manifest via
/// <see cref="Domain.Attributes.IntegrationActionAttribute"/>, resolved for one <see cref="IntegrationType"/>.
/// </summary>
public sealed record ActionDescriptor(
    string IntegrationId,
    string ActionId,
    string Label,
    string? Description,
    string? IconifyIcon,
    IReadOnlyList<ActionContext> Contexts,
    bool HasInput,
    bool SupportsDraft);

/// <summary>
/// The single indirection through which discovery and execution resolve actions (RFC 0012 §4.10). In
/// Phase 1 it joins the manifest-declared <see cref="IntegrationActionAttribute"/> metadata to the
/// DI-registered <see cref="IIntegrationAction"/> handlers, keyed by (<see cref="IntegrationType"/>,
/// ActionId). Nothing downstream — endpoints, frontend, persistence — asks the DI container or the
/// manifest directly, so a future plugin host can make this registry <i>also</i> return plugin-
/// contributed actions without touching them (the "plugin door" seam).
/// </summary>
public interface IActionRegistry
{
    /// <summary>All actions declared for <paramref name="type"/> (metadata + handler), in manifest order.</summary>
    IReadOnlyList<RegisteredAction> GetActions(string integrationId);

    /// <summary>Resolves a single action by (type, actionId), or null if none is declared/registered.</summary>
    RegisteredAction? Resolve(string integrationId, string actionId);

    /// <summary>Every registered action across all types — used by the manifest-honesty test and diagnostics.</summary>
    IReadOnlyList<RegisteredAction> All { get; }
}

using Piro.Contracts;

namespace Piro.Integrations.Abstractions;

/// <summary>
/// A registered UI action: its descriptor metadata joined to the handler that implements it (RFC 0012,
/// RFC 0016). Discovery, the executor, and the frontend read <see cref="Descriptor"/>; only the
/// executor touches <see cref="Handler"/>.
/// </summary>
public sealed record RegisteredUIAction(UIActionDescriptor Descriptor, IUIAction Handler);

/// <summary>
/// The metadata half of a UI action — everything the frontend and discovery need without touching the
/// handler. Built from the <see cref="IUIAction"/> the integration registered.
/// </summary>
public sealed record UIActionDescriptor(
    string IntegrationId,
    string ActionId,
    string Label,
    string? Description,
    string? IconifyIcon,
    IReadOnlyList<UISurface> Contexts,
    bool HasInput,
    bool SupportsDraft);

/// <summary>
/// The single indirection through which discovery and execution resolve UI actions (RFC 0012 §4.10,
/// RFC 0016). It holds the actions integrations registered imperatively via
/// <see cref="IUIExtensionHost.AddAction"/> at startup, keyed by (IntegrationId, ActionId). Nothing
/// downstream — endpoints, frontend, persistence — asks the DI container or an integration directly.
/// </summary>
public interface IUIActionRegistry
{
    /// <summary>All actions registered for <paramref name="integrationId"/> (metadata + handler).</summary>
    IReadOnlyList<RegisteredUIAction> GetActions(string integrationId);

    /// <summary>Resolves a single action by (integrationId, actionId), or null if none is registered.</summary>
    RegisteredUIAction? Resolve(string integrationId, string actionId);

    /// <summary>Every registered action across all integrations — used by diagnostics and honesty tests.</summary>
    IReadOnlyList<RegisteredUIAction> All { get; }

    /// <summary>Resolves the dynamic-options provider for (integrationId, sourceKey), or null if none is registered.</summary>
    IOptionsProvider? ResolveOptionsProvider(string integrationId, string sourceKey);
}

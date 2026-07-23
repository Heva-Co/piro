using Piro.Application.Integrations.Actions;

namespace Piro.Infrastructure.Integrations.Actions;

/// <summary>
/// The <see cref="IActionRegistry"/> (RFC 0012 §4.10, RFC 0016): indexes the DI-registered
/// <see cref="IIntegrationAction"/> handlers by (IntegrationId, ActionId). Each handler now
/// self-describes its metadata (label, contexts, icon), so the registry builds each
/// <see cref="ActionDescriptor"/> straight from the handler — no enum iteration, no manifest
/// attribute. Everything downstream asks this registry, never the DI container directly.
/// </summary>
internal sealed class ActionRegistry : IActionRegistry
{
    private readonly Dictionary<(string IntegrationId, string ActionId), RegisteredAction> _byKey = new();
    private readonly Dictionary<string, List<RegisteredAction>> _byIntegration = new(StringComparer.Ordinal);

    public ActionRegistry(IEnumerable<IIntegrationAction> handlers)
    {
        foreach (var handler in handlers)
        {
            var descriptor = new ActionDescriptor(
                handler.IntegrationId,
                handler.ActionId,
                handler.Label,
                handler.Description,
                handler.IconifyIcon,
                handler.Contexts,
                handler.HasInput,
                handler.SupportsDraft);

            var registered = new RegisteredAction(descriptor, handler);
            _byKey[(handler.IntegrationId, handler.ActionId)] = registered;
            if (!_byIntegration.TryGetValue(handler.IntegrationId, out var list))
                _byIntegration[handler.IntegrationId] = list = [];
            list.Add(registered);
        }
    }

    public IReadOnlyList<RegisteredAction> GetActions(string integrationId) =>
        _byIntegration.TryGetValue(integrationId, out var list) ? list : [];

    public RegisteredAction? Resolve(string integrationId, string actionId) =>
        _byKey.TryGetValue((integrationId, actionId), out var action) ? action : null;

    public IReadOnlyList<RegisteredAction> All => _byKey.Values.ToList();
}

using Piro.Application.Integrations.Actions;
using Piro.Domain.Attributes;
using Piro.Contracts;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Integrations.Actions;

/// <summary>
/// The Phase-1 <see cref="IActionRegistry"/> (RFC 0012 §4.10): joins the manifest-declared
/// <see cref="IntegrationActionAttribute"/> metadata on each <see cref="IntegrationType"/> to the
/// DI-registered <see cref="IIntegrationAction"/> handlers, keyed by (type, ActionId). Everything
/// downstream — discovery, execution, the frontend — asks this registry, never the DI container or the
/// manifest directly, so a future plugin host can extend it without touching them.
/// <para>
/// The join is the honesty invariant: a declared action with no handler (or a handler with no
/// declaration) is a wiring bug, asserted by a manifest-honesty test. Here we simply skip a declared
/// action that has no handler yet, so the system degrades safely while the layer is built out.
/// </para>
/// </summary>
internal sealed class ActionRegistry : IActionRegistry
{
    private readonly Dictionary<(IntegrationType Type, string ActionId), RegisteredAction> _byKey = new();
    private readonly Dictionary<IntegrationType, List<RegisteredAction>> _byType = new();

    public ActionRegistry(IEnumerable<IIntegrationAction> handlers)
    {
        var handlersByKey = handlers.ToDictionary(h => (h.Type, h.ActionId));

        foreach (var type in Enum.GetValues<IntegrationType>())
        {
            foreach (var meta in type.GetActions())
            {
                if (!handlersByKey.TryGetValue((type, meta.ActionId), out var handler))
                    continue; // declared but not yet implemented — skip until its handler lands

                var descriptor = new ActionDescriptor(
                    type,
                    meta.ActionId,
                    meta.Label,
                    meta.Description,
                    meta.IconifyIcon,
                    meta.Contexts,
                    meta.HasInput,
                    meta.SupportsDraft);

                var registered = new RegisteredAction(descriptor, handler);
                _byKey[(type, meta.ActionId)] = registered;
                if (!_byType.TryGetValue(type, out var list))
                    _byType[type] = list = [];
                list.Add(registered);
            }
        }
    }

    public IReadOnlyList<RegisteredAction> GetActions(IntegrationType type) =>
        _byType.TryGetValue(type, out var list) ? list : [];

    public RegisteredAction? Resolve(IntegrationType type, string actionId) =>
        _byKey.TryGetValue((type, actionId), out var action) ? action : null;

    public IReadOnlyList<RegisteredAction> All => _byKey.Values.ToList();
}

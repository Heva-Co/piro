using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Integrations.Actions;

/// <summary>
/// The concrete <see cref="IUIActionRegistry"/> and <see cref="IUIExtensionHost"/> in one object
/// (RFC 0016): a singleton populated once at startup. During startup each integration's
/// <c>Configure(host)</c> resolves this as <see cref="IUIExtensionHost"/> and calls
/// <see cref="AddAction"/>; afterwards discovery and the executor read it as
/// <see cref="IUIActionRegistry"/>. Registration is imperative (no DI scan of handlers) — the actions
/// present are exactly the ones integrations registered.
/// </summary>
internal sealed class UIActionRegistry : IUIActionRegistry, IUIExtensionHost
{
    private readonly Dictionary<(string IntegrationId, string ActionId), RegisteredUIAction> _byKey = new();
    private readonly Dictionary<string, List<RegisteredUIAction>> _byIntegration = new(StringComparer.Ordinal);
    private readonly Dictionary<(string IntegrationId, string SourceKey), IOptionsProvider> _optionsProviders = new();

    public void AddAction(IUIAction action)
    {
        var descriptor = new UIActionDescriptor(
            action.IntegrationId,
            action.ActionId,
            action.Label,
            action.Description,
            action.IconifyIcon,
            action.Contexts,
            action.HasInput,
            action.SupportsDraft);

        var registered = new RegisteredUIAction(descriptor, action);
        _byKey[(action.IntegrationId, action.ActionId)] = registered;
        if (!_byIntegration.TryGetValue(action.IntegrationId, out var list))
            _byIntegration[action.IntegrationId] = list = [];
        list.Add(registered);
    }

    public IReadOnlyList<RegisteredUIAction> GetActions(string integrationId) =>
        _byIntegration.TryGetValue(integrationId, out var list) ? list : [];

    public RegisteredUIAction? Resolve(string integrationId, string actionId) =>
        _byKey.TryGetValue((integrationId, actionId), out var action) ? action : null;

    public IReadOnlyList<RegisteredUIAction> All => _byKey.Values.ToList();

    public void AddOptionsProvider(IOptionsProvider provider) =>
        _optionsProviders[(provider.IntegrationId, provider.SourceKey)] = provider;

    public IOptionsProvider? ResolveOptionsProvider(string integrationId, string sourceKey) =>
        _optionsProviders.GetValueOrDefault((integrationId, sourceKey));
}

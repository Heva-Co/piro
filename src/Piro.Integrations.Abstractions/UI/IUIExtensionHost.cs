namespace Piro.Integrations.Abstractions;

/// <summary>
/// The UI-registration service an integration asks the host for during startup when it wants to
/// contribute to Piro's admin UI (RFC 0016). It is <b>not</b> passed to a running action — it is a
/// registrar: an integration resolves it via <c>host.GetRequiredService&lt;IUIExtensionHost&gt;()</c>
/// inside <see cref="IIntegration.Configure"/> and adds its buttons imperatively. Piro records what
/// each integration registered once at startup, then renders/executes from that registry.
/// <para>
/// Future UI surfaces (sections, tabs, widgets) add methods here without touching the base
/// <see cref="IIntegrationHost"/> — this is the "extends the UI" seam, kept separate from the root
/// integration host so an integration that contributes no UI never sees it.
/// </para>
/// </summary>
public interface IUIExtensionHost
{
    /// <summary>
    /// Registers a user-initiated action (a button) contributed by this integration. The action
    /// declares which surfaces it applies to via <see cref="IUIAction.Contexts"/>, so the host
    /// indexes it accordingly. Called from <see cref="IIntegration.Configure"/> at startup.
    /// </summary>
    void AddAction(IUIAction action);

    /// <summary>
    /// Registers a dynamic-options provider for a <c>[DynamicOptions(sourceKey)]</c> field on this
    /// integration's config form (e.g. Jira projects/issue types). Called from
    /// <see cref="IIntegration.Configure"/> at startup, alongside the integration's actions.
    /// </summary>
    void AddOptionsProvider(IOptionsProvider provider);
}

using Piro.Contracts;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Jira;

/// <summary>
/// Jira integration (RFC 0012, isolated per RFC 0016). A ThirdParty <b>action</b> integration: it does
/// not dispatch notifications — it contributes a user-initiated "Create Jira ticket" button to Alert,
/// Incident and Maintenance detail pages, and links the created ticket back to the Piro object.
/// <para>
/// Fully self-contained now: identity + manifest is pure data, and <see cref="Configure"/> registers the
/// create-issue action and its dynamic-options providers imperatively at startup. It reaches Piro only
/// through <see cref="IIntegrationHost"/> (OAuth token + its own config) and the UI registrar
/// <see cref="IUIExtensionHost"/>; nothing Jira touches a Piro repository or the DbContext.
/// </para>
/// </summary>
public sealed class JiraIntegration : IIntegration
{
    /// <summary>Stable identity of this integration type. Matches the former IntegrationType.Jira.</summary>
    public string IntegrationId => "Jira";

    /// <summary>
    /// ThirdParty integration that requires an OAuth connection and extends the UI with an action button.
    /// SupportedEvents is empty — Jira is not a notification target, so SubscribesToEvents is NOT set.
    /// </summary>
    public IntegrationManifest Manifest { get; } = new()
    {
        Capabilities = IntegrationCapability.RequiresOAuthConnection | IntegrationCapability.ExtendsUserInterface,
        ConfigType = typeof(JiraConfig),
        Label = "Jira",
        Description = "Create and track Jira tickets from alerts, incidents, and maintenances.",
        IconifyIcon = "logos:jira",
        SupportedEvents = [],
    };

    /// <summary>
    /// Registers Jira's UI contributions at startup: the "Create Jira ticket" action and the two
    /// dynamic-options providers (projects, issue types) its config form/dialog cascade off.
    /// </summary>
    public void Configure(IIntegrationHost host)
    {
        var ui = host.GetRequiredService<IUIExtensionHost>();
        ui.AddAction(new JiraCreateIssueAction());
        ui.AddOptionsProvider(new JiraProjectsOptionsProvider());
        ui.AddOptionsProvider(new JiraIssueTypesOptionsProvider());
    }
}

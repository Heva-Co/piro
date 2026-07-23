using Piro.Contracts;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Jira;

/// <summary>
/// Jira integration (RFC 0012, isolated per RFC 0016). A ThirdParty <b>action</b> integration: it does
/// not dispatch notifications — it contributes a user-initiated "Create Jira ticket" button to Alert,
/// Incident and Maintenance detail pages, and links the created ticket back to the Piro object.
/// <para>
/// Identity + manifest only. The action behavior (create-issue), its dynamic-options providers
/// (jira-projects, jira-issue-types), and the OAuth 3LO connection descriptor/discovery still live in
/// Infrastructure and are NOT moved here yet — they depend on host seams (IActionHost /
/// IOAuthTokenProvider) that the isolated abstractions do not yet expose. See the migration note.
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
    /// <remarks>
    /// The "create-issue" action's metadata (label, contexts, input/draft) and behavior still live with
    /// RFC 0012's action layer in Infrastructure, reached through <c>IActionHost</c>; this manifest only
    /// declares the <see cref="IntegrationCapability.ExtendsUserInterface"/> capability. Carrying action
    /// descriptors on the manifest is a follow-up once the action layer is folded in (see RFC 0016 §4.6).
    /// </remarks>
    public IntegrationManifest Manifest { get; } = new()
    {
        Category = IntegrationCategory.ThirdParty,
        Capabilities = IntegrationCapability.RequiresOAuthConnection | IntegrationCapability.ExtendsUserInterface,
        ConfigType = typeof(JiraConfig),
        Label = "Jira",
        Description = "Create and track Jira tickets from alerts, incidents, and maintenances.",
        IconifyIcon = "logos:jira",
        SupportedEvents = [],
    };
}

using Piro.Domain.Enums;

namespace Piro.Domain.Attributes;

/// <summary>
/// Declares everything known about an IntegrationType in one place: its category, direction,
/// capabilities, and ConfigJson shape. Consolidates what used to be three separate signals —
/// <c>IntegrationCategoryAttribute</c> (category/ChannelOnly), <c>RequiresIntegrationAttribute</c>
/// (via <see cref="IntegrationCapability.RequiredByCheckType"/>), and the previously-implicit
/// "does this type have a registered notification dispatcher" signal (via
/// <see cref="IntegrationCapability.SendsPersonalNotification"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class IntegrationManifestAttribute(
    IntegrationCategory category,
    IntegrationDirection direction,
    IntegrationCapability capabilities,
    Type configType
) : Attribute
{
    /// <summary>
    /// Whether this is a service/action integration (ThirdParty) or a notification channel (Notification).
    /// </summary>
    public IntegrationCategory Category { get; } = category;

    /// <summary>
    /// When true, this integration type does not store global credentials — configuration is
    /// provided per Notification Channel instead.
    /// </summary>
    public bool ChannelOnly { get; init; }

    /// <summary>
    /// Which way data flows through this Integration type — see <see cref="IntegrationDirection"/>.
    /// </summary>
    public IntegrationDirection Direction { get; } = direction;

    /// <summary>
    /// Concrete things this Integration type can do — see <see cref="IntegrationCapability"/>.
    /// <see cref="IntegrationCapability.None"/> is a valid, honest declaration for a type with no
    /// wired-up consumer yet.
    /// </summary>
    public IntegrationCapability Capabilities { get; } = capabilities;

    /// <summary>
    /// The class describing this type's ConfigJson shape — see <see cref="SecretFieldAttribute"/>
    /// and standard Data Annotations on its properties.
    /// </summary>
    public Type ConfigType { get; } = configType;

    /// <summary>
    /// Human-readable display name for the admin integrations picker (e.g. "Microsoft Teams" for
    /// <see cref="IntegrationType.MSTeams"/>). Falls back to the enum's own name when not set.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Short, human-readable description shown alongside this type's label in the admin
    /// integrations picker (e.g. "Sync issues and create tickets from alerts").
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Iconify icon name (e.g. "logos:jira") rendered for this type in the admin integrations
    /// picker. Just a string identifier — Domain has no dependency on the Iconify package itself.
    /// </summary>
    public string? IconifyIcon { get; init; }

    /// <summary>
    /// Whether an admin can create a new Integration of this type from the picker. False for
    /// <see cref="IntegrationType.Email"/> — it has a registered dispatcher (so it's a valid
    /// notification target) but is configured platform-wide (Settings &gt; Email), not by creating
    /// an Integration with its own ConfigJson. Distinct from <see cref="ChannelOnly"/>, which is
    /// about where credentials live, not whether the type is creatable here.
    /// </summary>
    public bool Creatable { get; init; } = true;

    /// <summary>
    /// Path segment (relative to <c>/api/v1/webhooks/</c>) this type's inbound endpoint listens on,
    /// e.g. <c>"gcp"</c> for <see cref="IntegrationType.GcpCloudMonitoringWebhook"/> — see
    /// <c>WebhooksController</c>. Only meaningful when <see cref="IntegrationCapability.CreatesAlerts"/>
    /// is set; null for every other type. Lets the admin form build the full webhook URL generically,
    /// without hardcoding a per-provider path.
    /// </summary>
    public string? WebhookPath { get; init; }
}

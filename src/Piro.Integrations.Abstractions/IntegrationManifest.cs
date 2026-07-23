namespace Piro.Integrations.Abstractions;

/// <summary>
/// Everything Piro knows about an integration, in one place: its category, capabilities, config
/// shape, presentation, the catalog events it handles, and where its inbound webhook (if any)
/// listens. Returned by <see cref="IIntegration.Manifest"/> — a plain data record, not an attribute
/// read by reflection (RFC 0016 §4.2), so it can carry non-constant values (e.g. a real
/// <see cref="SupportedEvents"/> array) and be constructed and tested directly.
/// </summary>
public sealed class IntegrationManifest
{
    /// <summary>Service/action integration (<see cref="IntegrationCategory.ThirdParty"/>) or notification channel (<see cref="IntegrationCategory.Notification"/>).</summary>
    public required IntegrationCategory Category { get; init; }

    /// <summary>The concrete things this integration can do — see <see cref="IntegrationCapability"/>. <see cref="IntegrationCapability.None"/> is a valid, honest declaration.</summary>
    public required IntegrationCapability Capabilities { get; init; }

    /// <summary>
    /// The class describing this integration's ConfigJson shape (config-field attributes + Data
    /// Annotations from Piro.Contracts on its properties). A <see cref="System.Type"/> — the
    /// abstractions assembly does not depend on Piro.Contracts for this; the config-schema engine
    /// reflects over it where the manifest is consumed.
    /// </summary>
    public required Type ConfigType { get; init; }

    /// <summary>Human-readable display name for the admin integrations picker (e.g. "Microsoft Teams").</summary>
    public string? Label { get; init; }

    /// <summary>Short description shown alongside the label in the admin integrations picker.</summary>
    public string? Description { get; init; }

    /// <summary>Iconify icon name (e.g. "logos:jira"). Just a string identifier — no Iconify dependency.</summary>
    public string? IconifyIcon { get; init; }

    /// <summary>
    /// True when this integration does not store global credentials — configuration is provided per
    /// Notification Channel instead.
    /// </summary>
    public bool ChannelOnly { get; init; }

    /// <summary>Whether an admin can create a new Integration of this type from the picker (false for Email — configured platform-wide).</summary>
    public bool Creatable { get; init; } = true;

    /// <summary>
    /// Path segment (relative to <c>/api/v1/webhooks/</c>) this integration's inbound endpoint listens
    /// on, e.g. <c>"gcp"</c>. Only meaningful when <see cref="IntegrationCapability.CreatesAlerts"/> is
    /// set; null otherwise. Lets the admin form build the full webhook URL generically.
    /// </summary>
    public string? WebhookPath { get; init; }

    /// <summary>
    /// The catalog events this integration can be subscribed to (RFC 0009), by their stable wire name
    /// (e.g. "alert:created", "incident:resolved"). Deliberately <see cref="string"/>[], not the core
    /// <c>NotificationEventType</c> enum: an integration declares which public events it handles
    /// without taking a dependency on Piro's event-catalog type ("integrations know nothing", §4.2b).
    /// Empty means the integration does not participate in event-subscriptions. A manifest-honesty
    /// test validates each name against the catalog and that this is non-empty iff
    /// <see cref="IntegrationCapability.SubscribesToEvents"/> is declared (RFC 0016 §4.5).
    /// </summary>
    public IReadOnlyList<string> SupportedEvents { get; init; } = [];

    /// <summary>
    /// Whether this integration handles the given catalog event wire name, honoring wildcards in
    /// <see cref="SupportedEvents"/> (RFC 0016): "*" matches everything, "alert:*" matches any
    /// "alert:…" event, and an exact name matches itself. Used by the create-time subscription guard
    /// and the UI catalog scoping.
    /// </summary>
    public bool HandlesEvent(string wireName)
    {
        foreach (var pattern in SupportedEvents)
        {
            if (pattern == "*" || pattern == wireName)
                return true;
            if (pattern.EndsWith(":*", StringComparison.Ordinal) &&
                wireName.StartsWith(pattern[..^1], StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Which way data flows — <b>derived</b> from <see cref="Capabilities"/>, not declared (RFC 0016
    /// §4.6): a type that creates alerts is inbound; one that sends/extends-UI is outbound. Kept as a
    /// projected value for the admin badge, with no hand-set field that could disagree with the
    /// capabilities beside it.
    /// </summary>
    public IntegrationDirection Direction => DeriveDirection(Capabilities);

    private static IntegrationDirection DeriveDirection(IntegrationCapability capabilities)
    {
        var inbound = capabilities.HasFlag(IntegrationCapability.CreatesAlerts);
        var outbound =
            capabilities.HasFlag(IntegrationCapability.SendsPersonalNotification) ||
            capabilities.HasFlag(IntegrationCapability.SendsChannelNotification) ||
            capabilities.HasFlag(IntegrationCapability.SendsAlertEvents) ||
            capabilities.HasFlag(IntegrationCapability.ExtendsUserInterface);

        return (inbound, outbound) switch
        {
            (true, true) => IntegrationDirection.Both,
            (true, false) => IntegrationDirection.Inbound,
            _ => IntegrationDirection.Outbound,
        };
    }
}

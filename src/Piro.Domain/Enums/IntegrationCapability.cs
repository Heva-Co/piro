namespace Piro.Domain.Enums;

/// <summary>
/// Concrete things an IntegrationType can do, declared in its <see cref="Attributes.IntegrationManifestAttribute"/>.
/// Additive metadata over facts that already exist in the running system (e.g. a dispatcher being
/// registered) — not an authorization mechanism. An empty set is a valid, honest declaration for a
/// type that has no wired-up consumer yet (e.g. PagerDuty, Jira today).
/// </summary>
[Flags]
public enum IntegrationCapability
{
    None = 0,

    /// <summary>Has a registered IPersonalNotificationDispatcher for this IntegrationType.</summary>
    SendsPersonalNotification = 1 << 0,

    /// <summary>Some ICheckExecutor requires an Integration of this type (see RequiresIntegrationAttribute).</summary>
    RequiredByCheckType = 1 << 1,

    /// <summary>An inbound webhook that produces Alert rows.</summary>
    CreatesAlerts = 1 << 2,

    /// <summary>Integration.EscalationPolicyId is meaningful for this type.</summary>
    SupportsEscalationPolicy = 1 << 3,

    /// <summary>An inbound type that can optionally anchor an incoming signal to a Check.</summary>
    SupportsCheckCorrelation = 1 << 4,

    /// <summary>
    /// Piro connects to this provider via OAuth before it can be used — the admin must
    /// complete a "Connect" flow that stores an encrypted token against the integration. Drives the
    /// Connect/Disconnect UI.
    /// </summary>
    RequiresOAuthConnection = 1 << 5,

    /// <summary>Has a registered ISystemEventDispatcher — sends trigger/resolve alert events to a shared incident channel (RFC 0004).</summary>
    SendsAlertEvents = 1 << 6,

    /// <summary>Has a registered IChannelNotificationDispatcher — posts notifications to a shared team channel (RFC 0009).</summary>
    SendsChannelNotification = 1 << 7,

    /// <summary>
    /// Has one or more actions declared via <see cref="Attributes.IntegrationActionAttribute"/>, each
    /// backed by a registered IIntegrationAction — user-initiated buttons on Alert/Incident/Maintenance
    /// detail pages (RFC 0012). A manifest-honesty test asserts this flag is set iff the type declares
    /// at least one action.
    /// </summary>
    ProvidesActions = 1 << 8,
}

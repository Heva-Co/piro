using Piro.Contracts;
namespace Piro.Integrations.Abstractions;

/// <summary>
/// Concrete things an integration can do, declared in its <see cref="IntegrationManifest"/>.
/// Additive metadata over facts that already exist in the running system (e.g. a dispatcher being
/// registered) — not an authorization mechanism. An empty set is a valid, honest declaration for a
/// type that has no wired-up consumer yet.
/// </summary>
[Flags]
public enum IntegrationCapability
{
    None = 0,

    /// <summary>Delivers notifications to a single person's handle — its <see cref="IIntegrationEventHandler"/> handles Personal-mode deliveries (RFC 0016).</summary>
    SendsPersonalNotification = 1 << 0,

    /// <summary>An inbound webhook that produces Alert rows.</summary>
    CreatesAlerts = 1 << 2,

    /// <summary>Integration.EscalationPolicyId is meaningful for this type.</summary>
    SupportsEscalationPolicy = 1 << 3,

    /// <summary>
    /// Piro connects to this provider via OAuth before it can be used — the admin must
    /// complete a "Connect" flow that stores an encrypted token against the integration. Drives the
    /// Connect/Disconnect UI.
    /// </summary>
    RequiresOAuthConnection = 1 << 5,

    /// <summary>Posts notifications to a shared team channel — its <see cref="IIntegrationEventHandler"/> handles Channel-mode deliveries (RFC 0009, RFC 0016).</summary>
    SendsChannelNotification = 1 << 7,

    /// <summary>
    /// Contributes something to the admin UI — today, one or more user-initiated actions (RFC 0012's
    /// buttons on Alert/Incident/Maintenance detail pages), each backed by a registered
    /// IUIExtension. Renamed from <c>ProvidesActions</c> (RFC 0016 §4.6) so the capability names
    /// the general "extends the UI" idea rather than one surface; future surfaces (sections, tabs,
    /// widgets) reuse this flag. A manifest-honesty test asserts it is set iff the integration declares
    /// at least one UI extension.
    /// </summary>
    ExtendsUserInterface = 1 << 8,

    /// <summary>
    /// Can be the destination of a notification event-subscription (RFC 0009). A hard precondition:
    /// an integration must declare this to be subscribable at all, and the manifest-honesty test
    /// asserts it is set iff <c>IntegrationManifest.SupportedEvents</c> is non-empty (RFC 0016 §4.5).
    /// </summary>
    SubscribesToEvents = 1 << 9,

    /// <summary>
    /// Ships one or more checks that are only available while this integration is registered
    /// (RFC 0016) — the integration returns them from <c>IIntegration.ProvidedChecks()</c> and Piro
    /// composes them into the check catalog. E.g. GoogleCloud ships the Cloud Run Job check. The
    /// manifest-honesty test asserts it is set iff <c>ProvidedChecks()</c> is non-empty.
    /// </summary>
    ProvidesChecks = 1 << 10,
}

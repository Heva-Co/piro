namespace Piro.Integrations.Abstractions;

/// <summary>
/// A self-describing integration (RFC 0016). Each integration is a class that declares its own
/// identity and manifest, discovered from an explicit compile-time registry rather than enumerated
/// in a central enum. This is the replacement for the old <c>IntegrationType</c> enum value +
/// <c>[IntegrationManifest]</c> attribute pairing: the integration's own class is now the single
/// place that says what it is.
/// <para>
/// Implementations are <b>pure data</b> — they hold no injected services and do nothing at
/// construction, so the registry can instantiate one and read its manifest cheaply and safely, even
/// at design time when the OpenAPI document is generated (§4.3). Behavior (sending, acting) lives in
/// the integration's dispatchers/actions, which receive what they need through
/// <see cref="IIntegrationHost"/> — never by reaching into Piro directly (§4.2b).
/// </para>
/// </summary>
public interface IIntegration
{
    /// <summary>
    /// Stable, permanent identifier — the discriminator persisted in every <c>Integration</c> row.
    /// Equals the current enum member name verbatim ("Jira", "Twilio", "GcpCloudMonitoringWebhook")
    /// so no stored data has to migrate. Immutable once shipped: renaming it orphans every stored
    /// integration of that type (RFC 0016 §3), like RFC 0009's permanent event wire names.
    /// </summary>
    string IntegrationId { get; }

    /// <summary>Everything Piro knows about this integration — see <see cref="IntegrationManifest"/>.</summary>
    IntegrationManifest Manifest { get; }
}

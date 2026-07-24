using Microsoft.Extensions.DependencyInjection;
using Piro.Checks.Abstractions;

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

    /// <summary>
    /// DI-registration hook (RFC 0016): called once while the container is still being built, so the
    /// integration can register its own services into the collection (e.g. GoogleCloud registers its
    /// OAuth token provider/cache that its check executor consumes). Runs before the container exists, so
    /// it takes the <see cref="IServiceCollection"/> rather than a live host. Default no-op for an
    /// integration that needs no extra services.
    /// <para>
    /// TODO: KNOWN GAP (RFC 0016): this hands the integration the raw collection, so a service it registers can
    /// currently take a constructor dependency outside the host allow-list (e.g. a repository or the
    /// DbContext), bypassing the "integrations know nothing" boundary that <see cref="IIntegrationHost"/>
    /// enforces at the read seam. Enforcing the allow-list at registration is hard (an integration's own
    /// services legitimately depend on each other) and is deferred; for now integrations are trusted here.
    /// </para>
    /// </summary>
    void ConfigureServices(IServiceCollection services) { }

    /// <summary>
    /// Runtime startup hook (RFC 0016): called once per integration after the container is built, so the
    /// integration can imperatively register what it contributes to the running app. An integration that
    /// adds UI resolves <c>host.GetRequiredService&lt;IUIExtensionHost&gt;()</c> here and calls
    /// <c>AddAction(...)</c>; one that contributes nothing imperative leaves the default no-op.
    /// <para>
    /// Reading <see cref="Manifest"/> stays free of both hooks — it is pure data the registry can read at
    /// design time (OpenAPI generation) without ever calling <see cref="Configure"/> or
    /// <see cref="ConfigureServices"/>, which run only in a live application.
    /// </para>
    /// </summary>
    void Configure(IIntegrationHost host) { }

    /// <summary>
    /// The checks this integration ships (RFC 0016). A provider integration whose data is probed by a
    /// check (e.g. GoogleCloud → the Cloud Run Job check) declares that check here, so the check lives in
    /// the integration's own assembly and is only available when the integration is registered — Piro
    /// core never hardcodes the check→integration link. Pure data, like <see cref="Manifest"/>: the
    /// registry reads it to compose the check catalog. Default empty for an integration that ships no check.
    /// </summary>
    IEnumerable<ICheck> ProvidedChecks() => [];
}

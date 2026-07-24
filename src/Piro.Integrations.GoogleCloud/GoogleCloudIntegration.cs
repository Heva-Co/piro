using Microsoft.Extensions.DependencyInjection;
using Piro.Checks.Abstractions;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.GoogleCloud;

/// <summary>
/// Google Cloud as a self-describing integration (RFC 0016). Unlike the notification integrations it is
/// a ThirdParty provider probed by a *check* (the Cloud Run Job check), so it declares no SupportedEvents
/// and has no IIntegrationEventHandler — it ships its identity, manifest, the OAuth token provider its
/// check uses, and the check itself. The whole GoogleCloud surface (integration + token provider + check)
/// lives in this one assembly, and the check is only available when this integration is registered.
/// </summary>
public sealed class GoogleCloudIntegration : IIntegration
{
    // Hardcoded, permanent discriminator — matches the former IntegrationType.GoogleCloud and the
    // "GoogleCloud" string persisted on every Integration row of this type (never rename it, RFC 0016 §3).
    public string IntegrationId => "GoogleCloud";

    public IntegrationManifest Manifest => new()
    {
        // Ships the Cloud Run Job check (see ProvidedChecks below), available only while this integration
        // is registered. Declared so the admin's integration detail shows it.
        Capabilities = IntegrationCapability.ProvidesChecks,
        ConfigType = typeof(GoogleCloudConfig),
        Label = "Google Cloud",
        Description = "Run Cloud Run Job checks against your GCP project.",
        IconifyIcon = "logos:google-cloud",
    };

    /// <summary>
    /// Registers the GCP OAuth token provider + its token cache, and allow-lists the token provider so
    /// the Cloud Run Job check may resolve it through the check host (the boundary stays closed to
    /// everything not declared here).
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<GcpTokenCache>();
        services.AddScoped<IGcpTokenProvider, GcpTokenProvider>();
        services.AddSingleton(CheckHostAllowedType.Of<IGcpTokenProvider>());
    }

    /// <summary>Ships the Cloud Run Job check — available only because this integration is registered.</summary>
    public IEnumerable<ICheck> ProvidedChecks() => [new GcpCloudRunJobCheck()];
}

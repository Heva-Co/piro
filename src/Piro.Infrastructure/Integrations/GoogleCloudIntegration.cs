using Piro.Domain.Integrations.Config;
using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Integrations;

/// <summary>
/// Google Cloud as a self-describing integration (RFC 0016). Unlike the notification integrations it
/// is a ThirdParty provider consumed by a *check* executor (the Cloud Run Job check), not a dispatcher,
/// so it lives in Piro.Infrastructure alongside that executor and declares no SupportedEvents. Identity
/// + manifest only; there is no IIntegrationEventHandler for it.
/// </summary>
internal sealed class GoogleCloudIntegration : IIntegration
{
    public string IntegrationId => "GoogleCloud";

    public IntegrationManifest Manifest => new()
    {
        Category = IntegrationCategory.ThirdParty,
        Capabilities = IntegrationCapability.RequiredByCheckType,
        ConfigType = typeof(GoogleCloudConfig),
        Label = "Google Cloud",
        Description = "Run Cloud Run Job checks against your GCP project.",
        IconifyIcon = "logos:google-cloud",
    };
}

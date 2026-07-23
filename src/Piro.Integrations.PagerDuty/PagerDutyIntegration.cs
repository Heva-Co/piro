using Piro.Integrations.Abstractions;

namespace Piro.Integrations.PagerDuty;

/// <summary>
/// PagerDuty integration (RFC 0004 / RFC 0016): pages the on-call team by opening and closing events
/// on PagerDuty's Events API v2. A third-party, OAuth-connected, system-event integration — it sends
/// alert lifecycle events (trigger/resolve) to a shared incident channel, and PagerDuty runs its own
/// escalation/paging. Piro only opens and closes.
/// <para>
/// This class is <b>pure data</b>: identity plus manifest, no injected services and no work at
/// construction, so the registry can read its manifest cheaply. The behavior (sending events, OAuth,
/// routing-key discovery) is not carried here — see the migration note: those pieces depend on core
/// services the isolated assembly cannot reference and remain host-mediated.
/// </para>
/// </summary>
public sealed class PagerDutyIntegration : IIntegration
{
    /// <summary>
    /// Stable discriminator persisted in every PagerDuty <c>Integration</c> row. Equals the legacy
    /// <c>IntegrationType.PagerDuty</c> enum member name verbatim, so no stored data has to migrate.
    /// </summary>
    public string IntegrationId => "PagerDuty";

    /// <inheritdoc />
    public IntegrationManifest Manifest => new()
    {
        Category = IntegrationCategory.ThirdParty,
        Capabilities = IntegrationCapability.RequiresOAuthConnection | IntegrationCapability.SendsAlertEvents,
        ConfigType = typeof(PagerDutyConfig),
        Label = "PagerDuty",
        Description = "Page your on-call team through PagerDuty.",
        IconifyIcon = "logos:pagerduty",
    };
}

using Piro.Application.Interfaces;
using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Integrations.Webhooks;

/// <summary>
/// The concrete <see cref="IWebhookHost"/> and inbound-handler lookup (RFC 0016): a singleton populated
/// once at startup. During startup an inbound integration resolves this as <see cref="IWebhookHost"/>
/// and calls <see cref="RegisterWebhook"/>; the webhooks endpoint reads it as
/// <see cref="IInboundWebhookRegistry"/> to resolve the handler by integration id and dispatch.
/// </summary>
internal sealed class WebhookRegistry(IIntegrationRegistry integrations) : IWebhookHost, IInboundWebhookRegistry
{
    private readonly Dictionary<string, IInboundWebhookHandler> _byIntegrationId = new(StringComparer.Ordinal);

    public void RegisterWebhook(IInboundWebhookHandler handler)
    {
        // Capability gate (RFC 0016): only an integration that declares CreatesAlerts may own a webhook.
        // Enforced here at startup — a bug in the integration's manifest fails loudly and once.
        var manifest = integrations.Find(handler.IntegrationId)?.Manifest
            ?? throw new InvalidOperationException(
                $"Webhook handler for unknown integration '{handler.IntegrationId}'.");
        if (!manifest.Capabilities.HasFlag(IntegrationCapability.CreatesAlerts))
            throw new InvalidOperationException(
                $"Integration '{handler.IntegrationId}' registered a webhook handler but does not declare " +
                "IntegrationCapability.CreatesAlerts (RFC 0016).");

        if (!_byIntegrationId.TryAdd(handler.IntegrationId, handler))
            throw new InvalidOperationException(
                $"Integration '{handler.IntegrationId}' already registered a webhook handler.");
    }

    /// <summary>The inbound handler for an integration type, or null if that type has no webhook.</summary>
    public IInboundWebhookHandler? Resolve(string integrationId) =>
        _byIntegrationId.GetValueOrDefault(integrationId);
}

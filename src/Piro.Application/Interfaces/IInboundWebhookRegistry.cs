using Piro.Integrations.Abstractions;

namespace Piro.Application.Interfaces;

/// <summary>
/// Lookup side of the inbound-webhook registry (RFC 0016): resolves the handler an integration type
/// registered at startup. The webhooks endpoint uses this to dispatch a request to the right handler.
/// The registration side is <see cref="IWebhookHost"/> (used by integrations in Configure).
/// </summary>
public interface IInboundWebhookRegistry
{
    /// <summary>The inbound handler for an integration type, or null if that type registered no webhook.</summary>
    IInboundWebhookHandler? Resolve(string integrationId);
}

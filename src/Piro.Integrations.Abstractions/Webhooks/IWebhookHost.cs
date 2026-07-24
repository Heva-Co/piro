namespace Piro.Integrations.Abstractions;

/// <summary>
/// The registrar an inbound integration asks the host for during startup to declare its webhook handler
/// (RFC 0016). Resolved via <c>host.GetRequiredService&lt;IWebhookHost&gt;()</c> inside
/// <see cref="IIntegration.Configure"/> — the same imperative pattern as <see cref="IUIExtensionHost"/>.
/// Piro indexes the handler by its <see cref="IInboundWebhookHandler.IntegrationId"/>; the inbound
/// endpoint resolves the integration instance from the URL's id, then dispatches to its handler.
/// </summary>
public interface IWebhookHost
{
    /// <summary>
    /// Registers the inbound webhook handler for this integration. Registering a second handler for an
    /// integration id that already has one is a startup error (RFC 0016).
    /// </summary>
    void RegisterWebhook(IInboundWebhookHandler handler);
}

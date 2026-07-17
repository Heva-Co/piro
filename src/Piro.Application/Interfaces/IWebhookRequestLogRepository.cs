using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>
/// Persistence contract for <see cref="WebhookRequestLog"/> rows
/// </summary>
public interface IWebhookRequestLogRepository
{
    Task<WebhookRequestLog> CreateAsync(WebhookRequestLog log, CancellationToken ct = default);
    Task<WebhookRequestLog> UpdateAsync(WebhookRequestLog log, CancellationToken ct = default);

    /// <summary>Returns the most recent requests for an Integration's webhook, newest first — the admin log viewer (RFC 0001 §4.4).</summary>
    Task<IEnumerable<WebhookRequestLog>> GetRecentByIntegrationIdAsync(Guid integrationId, int limit, CancellationToken ct = default);
}

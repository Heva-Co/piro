using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWebhookRequestLogRepository"/>.
/// </summary>
internal class WebhookRequestLogRepository(PiroDbContext db) : IWebhookRequestLogRepository
{
    public async Task<WebhookRequestLog> CreateAsync(WebhookRequestLog log, CancellationToken ct = default)
    {
        db.WebhookRequestLogs.Add(log);
        await db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<WebhookRequestLog> UpdateAsync(WebhookRequestLog log, CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<IEnumerable<WebhookRequestLog>> GetRecentByIntegrationIdAsync(Guid integrationId, int limit, CancellationToken ct = default)
    {
        return await db.WebhookRequestLogs
            .Where(l => l.IntegrationId == integrationId)
            .OrderByDescending(l => l.ReceivedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

}

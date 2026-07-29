using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Auditing;

/// <summary>Writes audit entries the interceptor cannot observe (issue #17).</summary>
internal class AuditLogWriter(PiroDbContext db, TimeProvider timeProvider) : IAuditLogWriter
{
    public async Task WriteAuthEventAsync(
        AuditAction action,
        string userId,
        string email,
        string? ipAddress,
        CancellationToken ct = default)
    {
        // Its own correlation id: an authentication event is a transaction of one, and grouping it
        // with whatever else the request happens to save would misattribute both.
        db.AuditLogs.Add(new AuditLog
        {
            CorrelationId = Guid.CreateVersion7(),
            IsPrimary = true,
            UserId = userId,
            UserEmail = email,
            Action = action,
            // No entity is involved. EntityType is left empty rather than filled with a placeholder
            // so a filter by entity type cannot accidentally match authentication events.
            EntityType = string.Empty,
            EntityId = string.Empty,
            IpAddress = ipAddress,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        });

        await db.SaveChangesAsync(ct);
    }
}

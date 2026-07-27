using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class DeviceTokenRepository(PiroDbContext db) : IDeviceTokenRepository
{
    public async Task<List<DeviceToken>> GetByUserIdAsync(int userId, CancellationToken ct = default) =>
        await db.DeviceTokens
            .Where(d => d.UserId == userId)
            .ToListAsync(ct);

    public async Task<DeviceToken> UpsertAsync(int userId, DevicePlatform platform, string token, string? deviceName, string? pushPublicKey, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existing = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == userId && d.Token == token, ct);
        if (existing is not null)
        {
            existing.Platform = platform;
            existing.DeviceName = deviceName;
            existing.LastSeenAt = now;
            existing.FailureCount = 0;
            // Only replace a stored key when the client actually sent one: an older client that omits
            // the field must not clear a key that is working.
            if (!string.IsNullOrWhiteSpace(pushPublicKey))
                existing.PushPublicKey = pushPublicKey;
            await db.SaveChangesAsync(ct);
            return existing;
        }

        var device = new DeviceToken
        {
            UserId = userId,
            Platform = platform,
            Token = token,
            DeviceName = deviceName,
            PushPublicKey = pushPublicKey,
            LastSeenAt = now,
        };
        db.DeviceTokens.Add(device);
        await db.SaveChangesAsync(ct);
        return device;
    }

    public async Task DeleteByTokenAsync(int userId, string token, CancellationToken ct = default) =>
        await db.DeviceTokens
            .Where(d => d.UserId == userId && d.Token == token)
            .ExecuteDeleteAsync(ct);

    public async Task PruneTokensAsync(IEnumerable<string> tokens, CancellationToken ct = default)
    {
        var list = tokens.Distinct().ToList();
        if (list.Count == 0) return;
        await db.DeviceTokens
            .Where(d => list.Contains(d.Token))
            .ExecuteDeleteAsync(ct);
    }

    public async Task IncrementFailureAsync(string token, CancellationToken ct = default) =>
        await db.DeviceTokens
            .Where(d => d.Token == token)
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.FailureCount, d => d.FailureCount + 1), ct);
}

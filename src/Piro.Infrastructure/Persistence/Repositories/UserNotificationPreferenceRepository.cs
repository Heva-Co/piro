using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class UserNotificationPreferenceRepository(PiroDbContext db) : IUserNotificationPreferenceRepository
{
    public async Task<List<UserNotificationPreference>> GetByUserIdAsync(int userId, CancellationToken ct = default) =>
        await db.UserNotificationPreferences
            .Include(p => p.Integration)
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Priority)
            .ToListAsync(ct);

    public async Task SetAsync(int userId, List<UserNotificationPreference> preferences, CancellationToken ct = default)
    {
        var existing = await db.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        db.UserNotificationPreferences.RemoveRange(existing);
        db.UserNotificationPreferences.AddRange(preferences);
        await db.SaveChangesAsync(ct);
    }
}

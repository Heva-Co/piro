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

    public async Task<Dictionary<int, List<UserNotificationPreference>>> GetByUserIdsAsync(
        IReadOnlyCollection<int> userIds, CancellationToken ct = default)
    {
        var prefs = await db.UserNotificationPreferences
            .Include(p => p.Integration)
            .Where(p => userIds.Contains(p.UserId))
            .OrderBy(p => p.Priority)
            .ToListAsync(ct);

        return prefs.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task<UserNotificationPreference?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.UserNotificationPreferences
            .Include(p => p.Integration)
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<UserNotificationPreference> CreateAsync(UserNotificationPreference preference, CancellationToken ct = default)
    {
        db.UserNotificationPreferences.Add(preference);
        await db.SaveChangesAsync(ct);
        return preference;
    }

    public async Task UpdateAsync(UserNotificationPreference preference, CancellationToken ct = default)
    {
        db.UserNotificationPreferences.Update(preference);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(UserNotificationPreference preference, CancellationToken ct = default)
    {
        db.UserNotificationPreferences.Remove(preference);
        await db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="INotificationChannelRepository"/>.</summary>
internal class NotificationChannelRepository(PiroDbContext db) : INotificationChannelRepository
{
    public async Task<IEnumerable<NotificationChannel>> GetAllAsync(CancellationToken ct = default) =>
        await db.NotificationChannels
            .Include(c => c.AlertConfigNotificationChannels)
            .Include(c => c.Integration)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    public async Task<IEnumerable<NotificationChannel>> GetGlobalAsync(CancellationToken ct = default) =>
        await db.NotificationChannels.Where(c => c.IsGlobal).ToListAsync(ct);

    public async Task<NotificationChannel?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.NotificationChannels
            .Include(c => c.Integration)
            .Include(c => c.AlertConfigNotificationChannels)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<NotificationChannel> CreateAsync(NotificationChannel channel, CancellationToken ct = default)
    {
        db.NotificationChannels.Add(channel);
        await db.SaveChangesAsync(ct);
        return channel;
    }

    public async Task<NotificationChannel> UpdateAsync(NotificationChannel channel, CancellationToken ct = default)
    {
        db.NotificationChannels.Update(channel);
        await db.SaveChangesAsync(ct);
        return channel;
    }

    public async Task DeleteAsync(NotificationChannel channel, CancellationToken ct = default)
    {
        db.NotificationChannels.Remove(channel);
        await db.SaveChangesAsync(ct);
    }
}

using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="NotificationChannel"/> entities.</summary>
public interface INotificationChannelRepository
{
    Task<IEnumerable<NotificationChannel>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<NotificationChannel>> GetGlobalAsync(CancellationToken ct = default);
    Task<NotificationChannel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<NotificationChannel> CreateAsync(NotificationChannel channel, CancellationToken ct = default);
    Task<NotificationChannel> UpdateAsync(NotificationChannel channel, CancellationToken ct = default);
    Task DeleteAsync(NotificationChannel channel, CancellationToken ct = default);
}

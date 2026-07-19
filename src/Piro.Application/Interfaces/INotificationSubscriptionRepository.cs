using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

public interface INotificationSubscriptionRepository
{
    Task<(IEnumerable<NotificationSubscription> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<NotificationSubscription?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<NotificationSubscription> CreateAsync(NotificationSubscription subscription, CancellationToken ct = default);
    Task<NotificationSubscription> UpdateAsync(NotificationSubscription subscription, CancellationToken ct = default);
    Task DeleteAsync(NotificationSubscription subscription, CancellationToken ct = default);

    /// <summary>All enabled subscriptions with their destination navigations loaded — for the matching processor.</summary>
    Task<IReadOnlyList<NotificationSubscription>> GetEnabledAsync(CancellationToken ct = default);
}

using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

public interface IUserNotificationPreferenceRepository
{
    Task<List<UserNotificationPreference>> GetByUserIdAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Batched variant of <see cref="GetByUserIdAsync"/> — fetches preferences for all given users
    /// in a single query and groups them by user id. Use this instead of calling
    /// <see cref="GetByUserIdAsync"/> in a loop (e.g. over on-call users for an escalation step),
    /// which would issue one round-trip per user.
    /// </summary>
    Task<Dictionary<int, List<UserNotificationPreference>>> GetByUserIdsAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default);
    Task<UserNotificationPreference?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<UserNotificationPreference> CreateAsync(UserNotificationPreference preference, CancellationToken ct = default);
    Task UpdateAsync(UserNotificationPreference preference, CancellationToken ct = default);
    Task DeleteAsync(UserNotificationPreference preference, CancellationToken ct = default);
}

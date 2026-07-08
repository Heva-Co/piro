using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

public interface IUserNotificationPreferenceRepository
{
    Task<List<UserNotificationPreference>> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task SetAsync(int userId, List<UserNotificationPreference> preferences, CancellationToken ct = default);
}

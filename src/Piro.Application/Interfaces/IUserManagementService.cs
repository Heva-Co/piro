using Piro.Application.DTOs;

namespace Piro.Application.Interfaces;

public interface IUserManagementService
{
    Task<List<UserListDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserListDto?> GetByIdAsync(int userId, CancellationToken ct = default);
    Task InviteAsync(string email, int roleId, CancellationToken ct = default);
    Task AcceptInviteAsync(string token, string name, string password, CancellationToken ct = default);
    Task ChangeRoleAsync(int userId, int newRoleId, CancellationToken ct = default);
    Task DeleteAsync(int userId, int currentUserId, CancellationToken ct = default);
    Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default);
    Task<UserProfileDto> GetProfileAsync(int userId, CancellationToken ct = default);
    Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task<List<UserNotificationPreferenceDto>> GetNotificationPreferencesAsync(int userId, CancellationToken ct = default);
    Task<List<UserNotificationPreferenceDto>> SetNotificationPreferencesAsync(int userId, SetUserNotificationPreferencesRequest request, CancellationToken ct = default);
}

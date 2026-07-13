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
    Task ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<List<UserNotificationPreferenceDto>> GetNotificationPreferencesAsync(int userId, CancellationToken ct = default);

    /// <summary>Creates a new preference, appended at the lowest priority.</summary>
    Task<UserNotificationPreferenceDto> CreateNotificationPreferenceAsync(int userId, UpsertUserNotificationPreferenceRequest request, CancellationToken ct = default);

    /// <summary>Edits an existing preference's channel/integration/handle. Resets verification if the handle or channel changed.</summary>
    Task<UserNotificationPreferenceDto> UpdateNotificationPreferenceAsync(int userId, int preferenceId, UpsertUserNotificationPreferenceRequest request, CancellationToken ct = default);

    /// <summary>Reassigns priority order from a full list of the user's preference ids in the desired order.</summary>
    Task<List<UserNotificationPreferenceDto>> ReorderNotificationPreferencesAsync(int userId, ReorderUserNotificationPreferencesRequest request, CancellationToken ct = default);

    /// <summary>Deletes a preference. Rejects the account-fallback preference.</summary>
    Task DeleteNotificationPreferenceAsync(int userId, int preferenceId, CancellationToken ct = default);

    /// <summary>Sends a one-time verification code to an already-saved (pending) preference's handle.</summary>
    Task SendNotificationPreferenceCodeAsync(int userId, int preferenceId, CancellationToken ct = default);

    /// <summary>Confirms a verification code, marking the preference verified if it matches.</summary>
    Task<UserNotificationPreferenceDto> ConfirmNotificationPreferenceCodeAsync(int userId, int preferenceId, string code, CancellationToken ct = default);
}

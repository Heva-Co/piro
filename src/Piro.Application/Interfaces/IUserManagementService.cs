using Piro.Application.DTOs;

namespace Piro.Application.Interfaces;

public interface IUserManagementService
{
    Task<List<UserListDto>> GetAllAsync(CancellationToken ct = default);
    Task InviteAsync(string email, int roleId, CancellationToken ct = default);
    Task AcceptInviteAsync(string token, string name, string password, CancellationToken ct = default);
    Task ChangeRoleAsync(int userId, int newRoleId, CancellationToken ct = default);
    Task DeleteAsync(int userId, CancellationToken ct = default);
    Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default);
}

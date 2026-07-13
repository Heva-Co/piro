using System.ComponentModel.DataAnnotations;

namespace Piro.Application.DTOs;

public record UserListDto(
    int Id,
    string Email,
    string Name,
    bool IsActive,
    bool IsPending,
    string[] Roles,
    DateTimeOffset CreatedAt
);

public record RoleDto(int Id, string Name);

public record InviteUserRequest([EmailAddress] string Email, int RoleId);

public record AcceptInviteRequest(string Token, string Name, string Password);

public record ChangeRoleRequest(int RoleId);

public record UserProfileDto(
    int Id,
    string Email,
    string Name,
    string Color,
    string TimeZone,
    string[] Roles,
    bool IsOidc
);

public record UpdateProfileRequest(
    string? Name,
    string? Color,
    string? TimeZone
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword
);

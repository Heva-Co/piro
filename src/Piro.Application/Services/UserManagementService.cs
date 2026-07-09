using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Application.Services;

public class UserManagementService(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    IEmailService emailService,
    IConfiguration configuration,
    ISiteConfigRepository siteConfigRepo,
    IIntegrationRepository integrationRepo,
    IUserNotificationPreferenceRepository prefRepo) : IUserManagementService
{
    private const string InvitationTokenPurpose = "Invitation";
    private static readonly TimeSpan InvitationExpiry = TimeSpan.FromHours(48);

    public async Task<List<UserListDto>> GetAllAsync(CancellationToken ct = default)
    {
        var users = userManager.Users.ToList();
        var result = new List<UserListDto>(users.Count);

        foreach (var user in users.OrderBy(u => u.CreatedAt))
        {
            var roles = await userManager.GetRolesAsync(user);
            // A user is "pending" if they have no password hash set (invited but not accepted yet)
            var isPending = string.IsNullOrEmpty(user.PasswordHash);
            result.Add(new UserListDto(
                user.Id,
                user.Email!,
                user.Name,
                user.IsActive,
                isPending,
                roles.ToArray(),
                user.CreatedAt));
        }

        return result;
    }

    public async Task InviteAsync(string email, int roleId, CancellationToken ct = default)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException($"A user with email '{email}' already exists.");

        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new InvalidOperationException($"Role {roleId} not found.");

        // Create inactive placeholder — no password yet
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            Name = string.Empty,
            IsActive = false,
            EmailConfirmed = false,
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create invitation placeholder: {errors}");
        }

        await userManager.AddToRoleAsync(user, role.Name!);

        // Generate invitation token and store expiry alongside it
        var token = await userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, InvitationTokenPurpose);
        var expiry = DateTimeOffset.UtcNow.Add(InvitationExpiry).ToUnixTimeSeconds().ToString();
        await userManager.SetAuthenticationTokenAsync(user, "Piro", "InvitationExpiry", expiry);

        var siteConfig = await siteConfigRepo.GetAsync(ct);
        var baseUrl = siteConfig.Url?.TrimEnd('/')
            ?? configuration["App:BaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:5173";
        var inviteUrl = $"{baseUrl}/invite/{Uri.EscapeDataString(token)}?userId={user.Id}";

        var html = BuildInvitationEmail(inviteUrl);
        await emailService.SendAsync(email, "You've been invited to Piro", html, ct);
    }

    public async Task AcceptInviteAsync(string token, string name, string password, CancellationToken ct = default)
    {
        // Find pending users (no password hash) and validate token
        var pendingUsers = userManager.Users
            .Where(u => !u.IsActive && u.PasswordHash == null)
            .ToList();

        AppUser? matched = null;
        foreach (var candidate in pendingUsers)
        {
            var valid = await userManager.VerifyUserTokenAsync(
                candidate, TokenOptions.DefaultProvider, InvitationTokenPurpose, token);

            if (!valid) continue;

            // Check expiry
            var expiryStr = await userManager.GetAuthenticationTokenAsync(candidate, "Piro", "InvitationExpiry");
            if (expiryStr is null || !long.TryParse(expiryStr, out var expiryUnix) ||
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiryUnix)
                throw new InvalidOperationException("Invitation has expired.");

            matched = candidate;
            break;
        }

        if (matched is null)
            throw new InvalidOperationException("Invalid or expired invitation token.");

        matched.Name = name;
        matched.IsActive = true;
        matched.EmailConfirmed = true;

        var addPasswordResult = await userManager.AddPasswordAsync(matched, password);
        if (!addPasswordResult.Succeeded)
        {
            var errors = string.Join("; ", addPasswordResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Password validation failed: {errors}");
        }

        await userManager.UpdateAsync(matched);
        // Remove the expiry token after use
        await userManager.RemoveAuthenticationTokenAsync(matched, "Piro", "InvitationExpiry");
        // Invalidate the invitation token so it can't be reused
        await userManager.UpdateSecurityStampAsync(matched);
    }

    public async Task ChangeRoleAsync(int userId, int newRoleId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found.");

        var newRole = await roleManager.FindByIdAsync(newRoleId.ToString())
            ?? throw new InvalidOperationException($"Role {newRoleId} not found.");

        var currentRoles = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, currentRoles);
        await userManager.AddToRoleAsync(user, newRole.Name!);
    }

    public async Task DeleteAsync(int userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found.");

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains("Owner"))
            throw new InvalidOperationException("The Owner account cannot be deleted.");

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to delete user: {errors}");
        }
    }

    public async Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default)
    {
        return roleManager.Roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name!))
            .ToList();
    }

    public async Task<UserProfileDto> GetProfileAsync(int userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found.");
        var roles = await userManager.GetRolesAsync(user);
        var isOidc = user.ExternalProvider is not null;
        return new UserProfileDto(user.Id, user.Email!, user.Name, user.Color, roles.ToArray(), isOidc);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found.");

        if (request.Name is not null) user.Name = request.Name;
        if (request.Color is not null) user.Color = request.Color;

        await userManager.UpdateAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        return new UserProfileDto(user.Id, user.Email!, user.Name, user.Color, roles.ToArray(), user.ExternalProvider is not null);
    }

    public async Task<List<UserNotificationPreferenceDto>> GetNotificationPreferencesAsync(int userId, CancellationToken ct = default)
    {
        var prefs = await prefRepo.GetByUserIdAsync(userId, ct);
        return prefs.Select(MapPref).ToList();
    }

    public async Task<List<UserNotificationPreferenceDto>> SetNotificationPreferencesAsync(int userId, SetUserNotificationPreferencesRequest request, CancellationToken ct = default)
    {
        var newPrefs = request.Preferences.Select(r => new UserNotificationPreference
        {
            UserId = userId,
            IntegrationId = r.IntegrationId,
            Handle = r.Handle,
            Priority = r.Priority,
        }).ToList();

        await prefRepo.SetAsync(userId, newPrefs, ct);
        return (await prefRepo.GetByUserIdAsync(userId, ct)).Select(MapPref).ToList();
    }

    private static UserNotificationPreferenceDto MapPref(UserNotificationPreference p) => new(
        p.Id,
        p.IntegrationId,
        p.Integration?.Name ?? p.IntegrationId.ToString(),
        p.Integration?.Type.ToString() ?? "",
        p.Handle,
        p.Priority
    );

    private static string BuildInvitationEmail(string inviteUrl) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0;padding:0;background:#f4f4f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="padding:40px 0;">
            <tr><td align="center">
              <table width="560" cellpadding="0" cellspacing="0" style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,.1);">
                <tr><td style="background:#18181b;padding:24px 32px;">
                  <span style="color:#fff;font-size:20px;font-weight:700;">Piro</span>
                </td></tr>
                <tr><td style="padding:32px;">
                  <h1 style="margin:0 0 12px;font-size:22px;font-weight:600;color:#18181b;">You've been invited</h1>
                  <p style="margin:0 0 24px;font-size:15px;color:#52525b;line-height:1.6;">
                    You have been invited to join a Piro monitoring workspace. Click the button below to set up your account.
                  </p>
                  <a href="{inviteUrl}" style="display:inline-block;padding:12px 24px;background:#18181b;color:#fff;text-decoration:none;border-radius:6px;font-size:15px;font-weight:500;">
                    Accept invitation
                  </a>
                  <p style="margin:24px 0 0;font-size:13px;color:#a1a1aa;">
                    This invitation link expires in 48 hours. If you did not expect this email, you can safely ignore it.
                  </p>
                  <hr style="margin:24px 0;border:none;border-top:1px solid #e4e4e7;">
                  <p style="margin:0;font-size:12px;color:#a1a1aa;">
                    Or copy this link:<br><span style="color:#52525b;word-break:break-all;">{inviteUrl}</span>
                  </p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}

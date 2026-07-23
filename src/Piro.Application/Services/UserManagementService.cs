using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Attributes;
using Piro.Contracts;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

public class UserManagementService(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    IEmailService emailService,
    IConfiguration configuration,
    ISiteConfigRepository siteConfigRepo,
    IIntegrationRepository integrationRepo,
    IUserNotificationPreferenceRepository prefRepo,
    IEnumerable<IVerificationCodeSender> codeSenders) : IUserManagementService
{
    private const string InvitationTokenPurpose = "Invitation";
    private const string NotificationVerificationPurpose = "NotifyChannelVerify";
    private static readonly TimeSpan InvitationExpiry = TimeSpan.FromHours(48);

    private readonly Dictionary<IntegrationType, IVerificationCodeSender> _codeSenders =
        codeSenders.ToDictionary(s => s.Type);

    public async Task<List<UserListDto>> GetAllAsync(CancellationToken ct = default)
    {
        var users = userManager.Users.ToList();
        var result = new List<UserListDto>(users.Count);

        foreach (var user in users.OrderBy(u => u.CreatedAt))
            result.Add(await MapUserAsync(user));

        return result;
    }

    public async Task<UserListDto?> GetByIdAsync(int userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : await MapUserAsync(user);
    }

    private async Task<UserListDto> MapUserAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        // A user is "pending" if they have no password hash set (invited but not accepted yet)
        var isPending = string.IsNullOrEmpty(user.PasswordHash);
        return new UserListDto(
            user.Id,
            user.Email!,
            user.Name,
            user.IsActive,
            isPending,
            roles.ToArray(),
            user.CreatedAt);
    }

    public async Task InviteAsync(string email, int roleId, CancellationToken ct = default)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException($"A user with email '{email}' already exists.");

        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new NotFoundException(nameof(AppRole), roleId);

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

        await emailService.SendInvitationAsync(email, inviteUrl, ct);
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

        // Every user gets one auto-created, always-present Email preference mirroring their
        // account address — already verified (the account itself was just confirmed), reorderable
        // like any other preference, but never deletable.
        await prefRepo.CreateAsync(new UserNotificationPreference
        {
            UserId = matched.Id,
            Channel = PersonalNotificationChannel.Email,
            Handle = matched.Email!,
            Priority = 0,
            VerifiedAt = DateTimeOffset.UtcNow,
            IsAccountFallback = true,
        }, ct);
    }

    public async Task ChangeRoleAsync(int userId, int newRoleId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException(nameof(AppUser), userId);

        var newRole = await roleManager.FindByIdAsync(newRoleId.ToString())
            ?? throw new NotFoundException(nameof(AppRole), newRoleId);

        var currentRoles = await userManager.GetRolesAsync(user);

        // Prevent removing Owner from the last remaining Owner — same lockout shape as the
        // SSO-only guard: there would be no account left able to grant Owner access again.
        if (currentRoles.Contains("Owner") && newRole.Name != "Owner")
        {
            var owners = await userManager.GetUsersInRoleAsync("Owner");
            if (owners.Count <= 1)
                throw new InvalidOperationException("Cannot change the role of the last remaining Owner.");
        }

        await userManager.RemoveFromRolesAsync(user, currentRoles);
        await userManager.AddToRoleAsync(user, newRole.Name!);
    }

    public async Task DeleteAsync(int userId, int currentUserId, CancellationToken ct = default)
    {
        if (userId == currentUserId)
            throw new InvalidOperationException("You cannot delete your own account.");

        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException(nameof(AppUser), userId);

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
            ?? throw new StaleIdentityException(userId);
        var roles = await userManager.GetRolesAsync(user);
        var isOidc = user.ExternalProvider is not null;
        return user.ToDto(roles.ToArray(), isOidc);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new StaleIdentityException(userId);

        if (request.Name is not null) user.Name = request.Name;
        if (request.Color is not null) user.Color = request.Color;
        if (request.TimeZone is not null)
        {
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZone, out _))
                throw new InvalidOperationException($"Unknown time zone '{request.TimeZone}'.");
            user.TimeZone = request.TimeZone;
        }

        await userManager.UpdateAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        return user.ToDto(roles.ToArray(), user.ExternalProvider is not null);
    }

    public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new StaleIdentityException(userId);

        if (user.ExternalProvider is not null)
            throw new InvalidOperationException("This account signs in via SSO and has no local password to change.");

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task MarkShowcaseSeenAsync(int userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new StaleIdentityException(userId);

        if (user.HasSeenShowcase) return;

        user.HasSeenShowcase = true;
        await userManager.UpdateAsync(user);
    }

    public async Task<List<UserNotificationPreferenceDto>> GetNotificationPreferencesAsync(int userId, CancellationToken ct = default)
    {
        var prefs = await prefRepo.GetByUserIdAsync(userId, ct);
        return prefs.Select(MapPref).ToList();
    }

    public async Task<UserNotificationPreferenceDto> CreateNotificationPreferenceAsync(int userId, UpsertUserNotificationPreferenceRequest request, CancellationToken ct = default)
    {
        var channel = ParseChannel(request.Channel);
        if (channel.RequiresIntegration() && request.IntegrationId is null)
            throw new DomainValidationException($"Channel '{channel}' requires a configured platform integration.");

        var existing = await prefRepo.GetByUserIdAsync(userId, ct);
        var priority = existing.Count > 0 ? existing.Max(p => p.Priority) + 1 : 0;

        var created = await prefRepo.CreateAsync(new UserNotificationPreference
        {
            UserId = userId,
            Channel = channel,
            IntegrationId = channel.RequiresIntegration() ? request.IntegrationId : null,
            Handle = request.Handle,
            Priority = priority,
        }, ct);

        return MapPref(await prefRepo.GetByIdAsync(created.Id, ct) ?? created);
    }

    public async Task<UserNotificationPreferenceDto> UpdateNotificationPreferenceAsync(int userId, int preferenceId, UpsertUserNotificationPreferenceRequest request, CancellationToken ct = default)
    {
        var pref = await prefRepo.GetByIdAsync(preferenceId, ct);
        if (pref is null || pref.UserId != userId)
            throw new NotFoundException(nameof(UserNotificationPreference), preferenceId.ToString());

        // The account-fallback row can't be re-channeled/re-handled from the client — its
        // Channel/Handle always mirror the account email, kept in sync elsewhere.
        if (pref.IsAccountFallback)
            throw new DomainValidationException("The account fallback preference cannot be edited.");

        var channel = ParseChannel(request.Channel);
        if (channel.RequiresIntegration() && request.IntegrationId is null)
            throw new DomainValidationException($"Channel '{channel}' requires a configured platform integration.");

        // A handle or channel change invalidates any prior verification.
        var handleChanged = pref.Handle != request.Handle || pref.Channel != channel;

        pref.Channel = channel;
        pref.IntegrationId = channel.RequiresIntegration() ? request.IntegrationId : null;
        pref.Handle = request.Handle;
        if (handleChanged) pref.VerifiedAt = null;

        await prefRepo.UpdateAsync(pref, ct);
        return MapPref(await prefRepo.GetByIdAsync(pref.Id, ct) ?? pref);
    }

    public async Task<List<UserNotificationPreferenceDto>> ReorderNotificationPreferencesAsync(int userId, ReorderUserNotificationPreferencesRequest request, CancellationToken ct = default)
    {
        var existing = await prefRepo.GetByUserIdAsync(userId, ct);
        var byId = existing.ToDictionary(p => p.Id);

        if (request.OrderedIds.Count != existing.Count || request.OrderedIds.Any(id => !byId.ContainsKey(id)))
            throw new DomainValidationException("Reorder request must include exactly the user's current preference ids.");

        for (var i = 0; i < request.OrderedIds.Count; i++)
        {
            var pref = byId[request.OrderedIds[i]];
            pref.Priority = i;
            await prefRepo.UpdateAsync(pref, ct);
        }

        return (await prefRepo.GetByUserIdAsync(userId, ct)).Select(MapPref).ToList();
    }

    public async Task DeleteNotificationPreferenceAsync(int userId, int preferenceId, CancellationToken ct = default)
    {
        var pref = await prefRepo.GetByIdAsync(preferenceId, ct);
        if (pref is null || pref.UserId != userId)
            throw new NotFoundException(nameof(UserNotificationPreference), preferenceId.ToString());

        if (pref.IsAccountFallback)
            throw new DomainValidationException("The account fallback preference cannot be deleted.");

        await prefRepo.DeleteAsync(pref, ct);
    }

    private static PersonalNotificationChannel ParseChannel(string channel) =>
        Enum.TryParse<PersonalNotificationChannel>(channel, out var parsed)
            ? parsed
            : throw new DomainValidationException($"Unknown notification channel '{channel}'.");

    public async Task SendNotificationPreferenceCodeAsync(int userId, int preferenceId, CancellationToken ct = default)
    {
        var pref = await prefRepo.GetByIdAsync(preferenceId, ct);
        if (pref is null || pref.UserId != userId)
            throw new NotFoundException(nameof(UserNotificationPreference), preferenceId.ToString());

        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException(nameof(AppUser), userId);

        if (!_codeSenders.TryGetValue(pref.Channel.ToIntegrationType(), out var codeSender))
            throw new DomainValidationException($"Channel '{pref.Channel}' does not support verification.");

        var code = await userManager.GenerateUserTokenAsync(
            user, TokenOptions.DefaultPhoneProvider, VerificationPurpose(pref.Channel, pref.Handle));

        var sent = await codeSender.SendCodeAsync(
            pref.Integration, pref.Handle, $"Your Piro verification code is {code}. It expires shortly.", ct);

        if (!sent)
            throw new DomainValidationException($"Failed to send a verification code via {pref.Channel} to '{pref.Handle}'.");
    }

    public async Task<UserNotificationPreferenceDto> ConfirmNotificationPreferenceCodeAsync(int userId, int preferenceId, string code, CancellationToken ct = default)
    {
        var pref = await prefRepo.GetByIdAsync(preferenceId, ct);
        if (pref is null || pref.UserId != userId)
            throw new NotFoundException(nameof(UserNotificationPreference), preferenceId.ToString());

        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException(nameof(AppUser), userId);

        var valid = await userManager.VerifyUserTokenAsync(
            user, TokenOptions.DefaultPhoneProvider, VerificationPurpose(pref.Channel, pref.Handle), code);
        if (!valid)
            throw new DomainValidationException("Invalid or expired verification code.");

        pref.VerifiedAt = DateTimeOffset.UtcNow;
        await prefRepo.UpdateAsync(pref, ct);
        return MapPref(pref);
    }

    /// <summary>Token purpose scoped to the exact channel+handle pair, so a code sent for one handle can't confirm a different one.</summary>
    private static string VerificationPurpose(PersonalNotificationChannel channel, string handle) =>
        $"{NotificationVerificationPurpose}:{channel}:{handle}";

    private static UserNotificationPreferenceDto MapPref(UserNotificationPreference p) => new(
        p.Id,
        p.Channel.ToString(),
        p.IntegrationId,
        p.Integration?.Name,
        p.Handle,
        p.Priority,
        p.VerifiedAt.HasValue,
        p.IsAccountFallback
    );

}

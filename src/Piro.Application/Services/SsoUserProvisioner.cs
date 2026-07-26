using Microsoft.AspNetCore.Identity;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Application.Services;

/// <summary>
/// Shared provisioning path for external SSO sign-ins. Extracted from OidcService so the
/// OIDC and SAML2 flows create/link users and issue tokens through identical logic.
/// </summary>
public class SsoUserProvisioner(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    ITokenService tokenService) : ISsoUserProvisioner
{
    public async Task<SignInResponse> ProvisionAndSignInAsync(
        ExternalUserInfo info,
        string providerId,
        string defaultRole,
        string? allowedDomains,
        CancellationToken ct = default)
    {
        EnforceAllowedDomains(info.Email, allowedDomains);
        var user = await UpsertUserAsync(info, providerId, defaultRole);
        return await BuildResponseAsync(user);
    }

    private static void EnforceAllowedDomains(string email, string? allowedDomains)
    {
        if (string.IsNullOrWhiteSpace(allowedDomains))
            return;

        var emailParts = email.Split('@');
        if (emailParts.Length != 2 || string.IsNullOrWhiteSpace(emailParts[0]) || string.IsNullOrWhiteSpace(emailParts[1]))
            throw new InvalidOperationException($"SSO identity returned a malformed email address: '{email}'.");

        var allowed = allowedDomains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var domain = emailParts[1];
        if (!allowed.Any(d => d.Equals(domain, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Email domain '@{domain}' is not allowed for this SSO provider.");
    }

    private async Task<AppUser> UpsertUserAsync(ExternalUserInfo info, string providerId, string defaultRole)
    {
        // Look up by ExternalId + ExternalProvider (existing SSO user)
        var existing = userManager.Users
            .FirstOrDefault(u => u.ExternalId == info.Subject && u.ExternalProvider == providerId);

        if (existing is not null)
        {
            // Keep name/email in sync
            if (existing.Name != info.Name || existing.Email != info.Email)
            {
                existing.Name = info.Name;
                existing.Email = info.Email;
                existing.UserName = info.Email;
                await userManager.UpdateAsync(existing);
            }
            return existing;
        }

        // First-time SSO login — check if local account with same email exists
        var byEmail = await userManager.FindByEmailAsync(info.Email);
        if (byEmail is not null)
        {
            // Link external identity to existing local account
            byEmail.ExternalId = info.Subject;
            byEmail.ExternalProvider = providerId;
            byEmail.IsActive = true;
            if (string.IsNullOrEmpty(byEmail.Name)) byEmail.Name = info.Name;
            await userManager.UpdateAsync(byEmail);
            return byEmail;
        }

        // Brand-new user — auto-provision
        var newUser = new AppUser
        {
            UserName = info.Email,
            Email = info.Email,
            Name = info.Name,
            ExternalId = info.Subject,
            ExternalProvider = providerId,
            IsActive = true,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(newUser);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        var role = await roleManager.FindByNameAsync(defaultRole) is not null ? defaultRole : "Member";
        await userManager.AddToRoleAsync(newUser, role);

        return newUser;
    }

    private async Task<SignInResponse> BuildResponseAsync(AppUser user)
    {
        var (accessToken, expires) = await tokenService.GenerateAccessTokenAsync(user);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        var expiresIn = (int)(expires - DateTime.UtcNow).TotalSeconds;

        return new SignInResponse(
            accessToken,
            refreshToken,
            expiresIn,
            new UserDto(user.Id, user.Email!, user.Name, roles));
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Application.Services;

/// <summary>
/// Implements self-service password recovery by mirroring the invitation flow's proven
/// mechanics (see <see cref="UserManagementService.InviteAsync"/> /
/// <see cref="UserManagementService.AcceptInviteAsync"/>): a custom-purpose Identity token
/// carried in the reset link, an explicit 1-hour expiry stored in AspNetUserTokens, and
/// security-stamp rotation for single-use. Every failure path is silent (request) or
/// generic (reset) so the endpoints never leak whether an account exists.
/// </summary>
public class PasswordResetService(
    UserManager<AppUser> userManager,
    ISiteConfigRepository siteConfigRepo,
    IEmailService emailService,
    IConfiguration configuration,
    IOidcService oidcService) : IPasswordResetService
{
    private const string PasswordResetTokenPurpose = "PasswordReset";
    private static readonly TimeSpan ResetExpiry = TimeSpan.FromHours(1);

    public async Task RequestResetAsync(string email, CancellationToken ct = default)
    {
        // Enumeration guard: every early return is silent, and the method always returns
        // normally so the controller can return 200 uniformly.
        if (await oidcService.GetSsoOnlyModeAsync(ct)) return;

        var user = await userManager.FindByEmailAsync(email);
        if (user is null) return;
        if (!user.IsActive) return;
        if (user.ExternalProvider is not null) return; // SSO account: no local password

        var token = await userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, PasswordResetTokenPurpose);
        var expiry = DateTimeOffset.UtcNow.Add(ResetExpiry).ToUnixTimeSeconds().ToString();
        await userManager.SetAuthenticationTokenAsync(user, "Piro", "PasswordResetExpiry", expiry);

        var siteConfig = await siteConfigRepo.GetAsync(ct);
        var baseUrl = siteConfig.Url?.TrimEnd('/')
            ?? configuration["App:BaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:5173";
        var resetUrl = $"{baseUrl}/admin/auth/reset-password?token={Uri.EscapeDataString(token)}&userId={user.Id}";

        await emailService.SendPasswordResetAsync(user.Email!, resetUrl, ct);
    }

    public async Task ResetAsync(int userId, string token, string newPassword, CancellationToken ct = default)
    {
        // SSO-only may have been turned on after the link was mailed — no local password
        // may be set in that mode. Generic message (no enumeration signal).
        if (await oidcService.GetSsoOnlyModeAsync(ct))
            throw new InvalidOperationException("Invalid or expired reset link.");

        var user = await userManager.FindByIdAsync(userId.ToString());
        // Generic message — do not distinguish "no such user" from "bad token".
        if (user is null || user.ExternalProvider is not null || !user.IsActive)
            throw new InvalidOperationException("Invalid or expired reset link.");

        var valid = await userManager.VerifyUserTokenAsync(
            user, TokenOptions.DefaultProvider, PasswordResetTokenPurpose, token);
        if (!valid)
            throw new InvalidOperationException("Invalid or expired reset link.");

        var expiryStr = await userManager.GetAuthenticationTokenAsync(user, "Piro", "PasswordResetExpiry");
        if (expiryStr is null || !long.TryParse(expiryStr, out var expiryUnix) ||
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiryUnix)
            throw new InvalidOperationException("Invalid or expired reset link.");

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Password validation failed: {errors}");
        }

        await userManager.RemoveAuthenticationTokenAsync(user, "Piro", "PasswordResetExpiry");
        await userManager.UpdateSecurityStampAsync(user); // single-use: invalidates the link + all sessions
    }
}

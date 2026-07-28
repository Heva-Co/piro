using Microsoft.AspNetCore.Identity;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>Application service for local authentication: sign-in, sign-out, token refresh.</summary>
public class AuthService(
    UserManager<AppUser> userManager,
    ITokenService tokenService)
{
    public async Task<SignInResponse> SignInAsync(SignInRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? throw new DomainValidationException("Invalid email or password.");

        if (!user.IsActive)
            throw new DomainValidationException("Account is disabled.");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
            throw new DomainValidationException("Invalid email or password.");

        return await BuildResponseAsync(user);
    }

    /// <summary>
    /// Signs out. With a <paramref name="refreshToken"/> (the caller's own), only that device's session
    /// is revoked, so the user's other devices stay signed in (RFC 0018). Without one, every session is
    /// revoked ("sign out everywhere") — the safe default when the client can't name its session.
    /// </summary>
    public async Task SignOutAsync(int userId, string? refreshToken = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
            await tokenService.RevokeRefreshTokenAsync(refreshToken, ct);
        else
            await tokenService.RevokeAllAsync(userId, ct);
    }

    public async Task<SignInResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        // Rotate: validates + revokes the presented session, returns its user.
        var user = await tokenService.RotateRefreshTokenAsync(request.RefreshToken, ct)
            ?? throw new DomainValidationException("Invalid or expired refresh token.");

        if (!user.IsActive)
            throw new DomainValidationException("Account is disabled.");

        return await BuildResponseAsync(user);
    }

    /// <summary>
    /// Issues a session for a user the caller has already authenticated by some other means — today,
    /// a redeemed CLI authorization code (RFC 0019 §4.6). The <paramref name="deviceLabel"/> is what
    /// makes it appear in the sessions list as something the user can recognise and revoke.
    /// </summary>
    /// <remarks>
    /// A CLI login is not a new kind of credential: it is one more refresh-token session, with the
    /// same rotation, expiry and revocation as a browser one.
    /// </remarks>
    public Task<SignInResponse> IssueSessionAsync(AppUser user, string? deviceLabel, CancellationToken ct = default) =>
        BuildResponseAsync(user, deviceLabel, ct);

    private async Task<SignInResponse> BuildResponseAsync(
        AppUser user, string? deviceLabel = null, CancellationToken ct = default)
    {
        var (accessToken, expires) = await tokenService.GenerateAccessTokenAsync(user);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(user, deviceLabel, ct);
        var roles = await userManager.GetRolesAsync(user);
        var expiresIn = (int)(expires - DateTime.UtcNow).TotalSeconds;

        return new SignInResponse(
            accessToken,
            refreshToken,
            expiresIn,
            new UserDto(user.Id, user.Email!, user.Name, roles));
    }
}

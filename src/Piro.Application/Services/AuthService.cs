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

    public async Task SignOutAsync(int userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException(nameof(AppUser), userId.ToString());

        await userManager.RemoveAuthenticationTokenAsync(user, "Piro", "RefreshToken");
    }

    public async Task<SignInResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var user = await tokenService.ValidateRefreshTokenAsync(userManager, request.RefreshToken)
            ?? throw new DomainValidationException("Invalid or expired refresh token.");

        if (!user.IsActive)
            throw new DomainValidationException("Account is disabled.");

        // Rotate: revoke old token and issue new pair
        await userManager.RemoveAuthenticationTokenAsync(user, "Piro", "RefreshToken");
        return await BuildResponseAsync(user);
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

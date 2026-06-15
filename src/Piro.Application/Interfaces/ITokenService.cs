using Microsoft.AspNetCore.Identity;
using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Generates and validates JWT access tokens and refresh tokens.</summary>
public interface ITokenService
{
    Task<(string token, DateTime expires)> GenerateAccessTokenAsync(AppUser user);
    Task<string> GenerateRefreshTokenAsync(AppUser user);
    Task<AppUser?> ValidateRefreshTokenAsync(UserManager<AppUser> userManager, string refreshToken);
}

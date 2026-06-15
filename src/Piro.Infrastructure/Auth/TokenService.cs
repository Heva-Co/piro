using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Auth;

/// <summary>Generates and validates JWT access tokens and opaque refresh tokens.</summary>
public class TokenService(IConfiguration config, UserManager<AppUser> userManager) : ITokenService
{
    private readonly string _secret = config["Auth:JwtSecret"]
        ?? throw new InvalidOperationException("Auth:JwtSecret is required.");
    private readonly int _accessExpiryMinutes = int.TryParse(config["Auth:AccessTokenExpiryMinutes"], out var v) ? v : 60;

    /// <summary>Creates a signed JWT for the given user, including their roles as claims.</summary>
    public async Task<(string token, DateTime expires)> GenerateAccessTokenAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("name", user.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_accessExpiryMinutes);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    /// <summary>Generates a cryptographically random refresh token and stores it via Identity's token store.</summary>
    public async Task<string> GenerateRefreshTokenAsync(AppUser user)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        await userManager.SetAuthenticationTokenAsync(user, "Piro", "RefreshToken", token);
        return token;
    }

    /// <summary>Validates a refresh token and returns the owning user, or null if invalid.</summary>
    public async Task<AppUser?> ValidateRefreshTokenAsync(UserManager<AppUser> um, string refreshToken)
    {
        // Refresh tokens are stored per-user; we scan active users.
        // For scale, store a hashed index — fine for MVP.
        var users = um.Users.Where(u => u.IsActive).ToList();
        foreach (var user in users)
        {
            var stored = await um.GetAuthenticationTokenAsync(user, "Piro", "RefreshToken");
            if (stored == refreshToken) return user;
        }
        return null;
    }
}

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Auth;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

/// <summary>
/// Authenticates requests carrying an <c>X-Api-Key</c> header against
/// <see cref="ApiKeyService"/>, producing the same NameIdentifier/Role claims
/// a JWT would carry so existing [Authorize(Roles = "...")] checks work unchanged.
/// </summary>
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApiKeyService apiKeyService,
    UserManager<AppUser> userManager)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var headerValues))
            return AuthenticateResult.NoResult();

        var rawKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(rawKey))
            return AuthenticateResult.NoResult();

        var userId = await apiKeyService.ValidateAsync(rawKey);
        if (userId is null)
            return AuthenticateResult.Fail("Invalid or revoked API key.");

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null || !user.IsActive)
            return AuthenticateResult.Fail("API key's owning user is inactive or no longer exists.");

        var roles = await userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("name", user.Name),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.Scheme);

        return AuthenticateResult.Success(ticket);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Infrastructure.Auth;

namespace Piro.Api.Controllers;

/// <summary>Local authentication: sign-in, sign-out, and token refresh.</summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController(AuthService authService, ApiKeyService apiKeyService, IOidcService oidcService, IUserManagementService userService) : ControllerBase
{
    /// <summary>Authenticates with email and password, returns JWT + refresh token.</summary>
    [HttpPost("sign-in")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request, CancellationToken ct)
    {
        if (await oidcService.GetSsoOnlyModeAsync(ct))
            return StatusCode(423, new { title = "Password sign-in is disabled. Use SSO to sign in.", status = 423 });

        return Ok(await authService.SignInAsync(request, ct));
    }

    /// <summary>Invalidates the current refresh token.</summary>
    [HttpPost("sign-out")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignOut(CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await authService.SignOutAsync(userId, ct);
        return NoContent();
    }

    /// <summary>Exchanges a refresh token for a new access token pair.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct) =>
        Ok(await authService.RefreshAsync(request, ct));

    // ── Profile ──────────────────────────────────────────────────────────────

    /// <summary>Returns the authenticated user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await userService.GetProfileAsync(userId, ct));
    }

    /// <summary>Updates the authenticated user's display name and/or color.</summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await userService.UpdateProfileAsync(userId, request, ct));
    }

    /// <summary>Changes the authenticated user's password. Rejects SSO accounts, which have no local password.</summary>
    [HttpPut("me/password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            await userService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    // ── API Keys ─────────────────────────────────────────────────────────────

    /// <summary>Lists API keys for the authenticated user. Owner/Admin only.</summary>
    [HttpGet("api-keys")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<IEnumerable<ApiKeyDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApiKeys(CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await apiKeyService.GetByUserAsync(userId, ct));
    }

    /// <summary>Creates a new API key. The raw key is returned once and cannot be retrieved again. Owner/Admin only.</summary>
    [HttpPost("api-keys")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<ApiKeyCreatedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateApiKey([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var created = await apiKeyService.CreateAsync(userId, request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Revokes an API key. Owner/Admin only.</summary>
    [HttpDelete("api-keys/{id:int}")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeApiKey(int id, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await apiKeyService.RevokeAsync(id, userId, ct);
        return NoContent();
    }
}

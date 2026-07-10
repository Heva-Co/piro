using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Api.Controllers;

/// <summary>Handles OIDC/OAuth2 sign-in flows (start, callback) and exposes enabled providers to the UI.</summary>
[ApiController]
[Route("api/v1/auth/oidc")]
[Produces("application/json")]
[AllowAnonymous]
public class OidcController(IOidcService oidcService) : ControllerBase
{
    /// <summary>Returns OIDC providers enabled for the sign-in page.</summary>
    [HttpGet("providers")]
    [ProducesResponseType<List<OidcProviderInfo>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProviders(CancellationToken ct) =>
        Ok(await oidcService.GetEnabledProvidersAsync(ct));

    /// <summary>Returns whether SSO-only mode is active (used by the sign-in page to hide password form).</summary>
    [HttpGet("sso-mode")]
    [ProducesResponseType<object>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSsoMode(CancellationToken ct) =>
        Ok(new { ssoOnly = await oidcService.GetSsoOnlyModeAsync(ct) });

    /// <summary>Initiates the OIDC authorization code + PKCE flow for the given provider.</summary>
    [HttpGet("start")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Start([FromQuery] string provider, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BadRequest(new { title = "Provider is required.", status = 400 });

        try
        {
            var url = await oidcService.GetStartUrlAsync(provider, ct);
            return Redirect(url);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    /// <summary>
    /// SPA callback: exchanges authorization code for Piro tokens and returns JSON.
    /// Called by the Vite SPA after Google redirects to /admin/auth/oidc/callback.
    /// </summary>
    [HttpPost("callback")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CallbackPost([FromBody] OidcCallbackRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Code) || string.IsNullOrEmpty(request.State))
            return BadRequest(new { title = "code and state are required.", status = 400 });

        try
        {
            var response = await oidcService.HandleCallbackAsync(request.Code, request.State, ct);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }
}

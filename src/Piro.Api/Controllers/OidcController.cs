using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Api.Controllers;

/// <summary>Handles OIDC/OAuth2 sign-in flows (start, callback) and exposes enabled providers to the UI.</summary>
[ApiController]
[Route("api/v1/auth/oidc")]
[Produces("application/json")]
[AllowAnonymous]
public class OidcController(IOidcService oidcService, IConfiguration configuration, ISiteConfigRepository siteConfigRepo) : ControllerBase
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
    /// OAuth2 callback endpoint. Exchanges the authorization code, upserts the user,
    /// issues Piro tokens, and redirects to the frontend callback page.
    /// </summary>
    [HttpGet("callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        var siteConfig = await siteConfigRepo.GetAsync(ct);
        var frontendUrl = (siteConfig.Url?.TrimEnd('/'))
            ?? configuration["App:BaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:5173";
        var errorRedirect = $"{frontendUrl}/auth/sign-in?oidc_error=1";

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect(errorRedirect);

        try
        {
            var response = await oidcService.HandleCallbackAsync(code, state, ct);
            var callbackUrl = $"{frontendUrl}/auth/oidc/complete" +
                $"?token={Uri.EscapeDataString(response.AccessToken)}" +
                $"&refresh={Uri.EscapeDataString(response.RefreshToken)}" +
                $"&expires={response.ExpiresIn}";
            return Redirect(callbackUrl);
        }
        catch (Exception)
        {
            return Redirect(errorRedirect);
        }
    }
}

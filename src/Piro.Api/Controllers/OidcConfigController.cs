using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Api.Controllers;

/// <summary>Admin endpoints for managing OIDC/SSO provider configurations. Owner-only.</summary>
[ApiController]
[Route("api/v1/oidc/config")]
[Produces("application/json")]
[Authorize(Roles = "Owner")]
public class OidcConfigController(IOidcService oidcService) : ControllerBase
{
    /// <summary>Lists all configured OIDC providers (no client secrets).</summary>
    [HttpGet]
    [ProducesResponseType<List<OidcProviderConfigDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await oidcService.GetAllConfigsAsync(ct));

    /// <summary>Creates or updates an OIDC provider configuration.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert([FromBody] UpsertOidcProviderRequest request, CancellationToken ct)
    {
        try
        {
            await oidcService.UpsertConfigAsync(request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    /// <summary>Returns SSO-only mode setting.</summary>
    [HttpGet("sso-mode")]
    [ProducesResponseType<object>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSsoMode(CancellationToken ct) =>
        Ok(new { ssoOnly = await oidcService.GetSsoOnlyModeAsync(ct) });

    /// <summary>Enables or disables SSO-only mode (disables password sign-in).</summary>
    [HttpPut("sso-mode")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetSsoMode([FromBody] SetSsoModeRequest request, CancellationToken ct)
    {
        await oidcService.SetSsoOnlyModeAsync(request.SsoOnly, ct);
        return NoContent();
    }

    /// <summary>Tests an OIDC provider by fetching its discovery document.</summary>
    [HttpPost("test")]
    [ProducesResponseType<object>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Test([FromBody] TestOidcRequest request, CancellationToken ct)
    {
        try
        {
            var ok = await oidcService.TestProviderAsync(request.ProviderId, ct);
            return Ok(new { success = ok, message = ok ? "Provider is reachable." : "Provider check failed." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}

public record TestOidcRequest(string ProviderId);
public record SetSsoModeRequest(bool SsoOnly);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Api.Controllers;

/// <summary>Handles SAML 2.0 sign-in flows (start, assertion consumer service) and exposes enabled providers to the UI.</summary>
[ApiController]
[Route("api/v1/auth/saml")]
[AllowAnonymous]
public class Saml2Controller(ISaml2Service saml2Service) : ControllerBase
{
    /// <summary>Returns SAML providers enabled for the sign-in page.</summary>
    [HttpGet("providers")]
    [Produces("application/json")]
    [ProducesResponseType<List<Saml2ProviderInfo>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProviders(CancellationToken ct) =>
        Ok(await saml2Service.GetEnabledProvidersAsync(ct));

    /// <summary>Initiates the SP-initiated SAML redirect flow: sends an AuthnRequest to the IdP.</summary>
    [HttpGet("start")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Start([FromQuery] string provider, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BadRequest(new { title = "Provider is required.", status = 400 });

        try
        {
            var url = await saml2Service.GetStartUrlAsync(provider, ct);
            return Redirect(url);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    /// <summary>
    /// Assertion Consumer Service. The IdP POSTs the SAMLResponse here (form-encoded). On success
    /// the browser is redirected to the SPA callback with the issued tokens in the URL fragment
    /// (fragment keeps them out of server/referer logs); the SPA persists them and lands the user.
    /// </summary>
    [HttpPost("acs")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> AssertionConsumerService(
        [FromForm(Name = "SAMLResponse")] string samlResponse,
        [FromForm(Name = "RelayState")] string? relayState,
        CancellationToken ct)
    {
        try
        {
            var result = await saml2Service.HandleAcsAsync(samlResponse, relayState, ct);
            var s = result.SignIn;
            var fragment =
                $"access_token={Uri.EscapeDataString(s.AccessToken)}" +
                $"&refresh_token={Uri.EscapeDataString(s.RefreshToken)}" +
                $"&expires_in={s.ExpiresIn}";
            return Redirect($"/admin/auth/saml/callback#{fragment}");
        }
        catch (Exception)
        {
            return Redirect("/admin/auth/sign-in?saml_error=1");
        }
    }
}

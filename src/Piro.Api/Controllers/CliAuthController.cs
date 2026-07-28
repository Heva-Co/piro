using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Domain.Entities;

namespace Piro.Api.Controllers;

/// <summary>Body of <c>POST /api/v1/auth/cli/authorize</c>, sent by the consent screen.</summary>
public record CliAuthorizeBody(
    string RedirectUri,
    string CodeChallenge,
    string State,
    string? ClientLabel);

/// <summary>The one-time code the browser hands back to the CLI on the loopback redirect.</summary>
public record CliAuthorizeResponse(string Code, string State);

/// <summary>Body of <c>POST /api/v1/auth/cli/token</c>, sent by the CLI.</summary>
public record CliTokenBody(string Code, string CodeVerifier, string RedirectUri);

/// <summary>
/// The browser half of <c>piro login</c> (RFC 0019 §4.6): the admin panel authorizes a CLI, and the
/// CLI exchanges the resulting code for an ordinary refresh-token session.
/// </summary>
/// <remarks>
/// Kept in its own controller rather than folded into <see cref="AuthController"/> because it is the
/// most security-sensitive surface in this RFC and benefits from being read as one piece. It adds no
/// new authentication mechanism: what comes out is the same per-device session RFC 0018 already
/// manages, labelled so the user can see and revoke it.
/// </remarks>
[ApiController]
[Route("api/v1/auth/cli")]
[Produces("application/json")]
public class CliAuthController(
    ICliAuthService cliAuth,
    AuthService authService,
    UserManager<AppUser> userManager) : ControllerBase
{
    /// <summary>
    /// Mints a one-time code for the signed-in user. Called by the <c>/cli-auth</c> screen only after
    /// the user clicks Authorize — never on page load, or merely opening a link would grant a token.
    /// </summary>
    /// <response code="200">The code and the state to echo back to the CLI.</response>
    /// <response code="400">The callback is not a loopback address, or PKCE fields are missing.</response>
    [HttpPost("authorize")]
    [Authorize]
    [ProducesResponseType<CliAuthorizeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Authorize([FromBody] CliAuthorizeBody body, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Unauthorized();

        // Validated server-side as well as in the UI: the consent screen is a client, and a client's
        // check is a courtesy. This is what stops a crafted link forwarding a token elsewhere.
        if (!cliAuth.IsLoopback(body.RedirectUri))
            return BadRequest(new { title = "The CLI callback must be a loopback address.", status = 400 });

        var code = await cliAuth.IssueCodeAsync(
            user,
            new CliAuthorizeRequest(body.RedirectUri, body.CodeChallenge, body.State, body.ClientLabel),
            ct);

        return Ok(new CliAuthorizeResponse(code, body.State));
    }

    /// <summary>
    /// Exchanges a code plus its PKCE verifier for a session. Anonymous by design — the CLI has no
    /// credential yet; the code and the verifier together are what prove the claim.
    /// </summary>
    /// <response code="200">An access token and a refresh token, labelled as a CLI session.</response>
    /// <response code="400">The code is unknown, expired, already used, or failed verification.</response>
    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Token([FromBody] CliTokenBody body, CancellationToken ct)
    {
        var redeemed = await cliAuth.RedeemCodeAsync(
            new CliTokenRequest(body.Code, body.CodeVerifier, body.RedirectUri), ct);

        // One message for every failure mode. Distinguishing "expired" from "already used" from
        // "bad verifier" would tell an attacker which half of the exchange they got right.
        if (redeemed is not { } session)
            return BadRequest(new { title = "Invalid or expired authorization code.", status = 400 });

        // The label is what the user sees in their sessions list, so it carries the machine the CLI
        // reported. Untrusted display text: stored truncated and never interpreted.
        var label = string.IsNullOrWhiteSpace(session.ClientLabel) ? "piro-cli" : session.ClientLabel;
        return Ok(await authService.IssueSessionAsync(session.User, label, ct));
    }
}

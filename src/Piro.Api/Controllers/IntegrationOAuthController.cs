using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Infrastructure.Integrations.OAuth;

namespace Piro.Api.Controllers;

/// <summary>
/// Drives the OAuth connect flow for outbound integrations (RFC 0004): the admin starts a
/// connection for an integration, is redirected to the provider, and the callback exchanges the
/// code for tokens which are stored encrypted against that integration.
/// </summary>
[Authorize(Roles = "Owner,Admin")]
[ApiController]
[Route("api/v1/integrations/oauth")]
[Produces("application/json")]
public class IntegrationOAuthController(
    IOAuthClient oauthClient,
    IOAuthTokenStore tokenStore,
    IIntegrationRepository integrationRepo,
    IPagerDutyDiscoveryService pagerDutyDiscovery) : ControllerBase
{
    /// <summary>
    /// Lists the remote resources (PagerDuty services) discoverable for an OAuth-connected integration,
    /// live from the provider (RFC 0004 §4.4a) — never cached, so a service renamed/deleted upstream is
    /// reflected immediately. Only the admin's chosen mapping is persisted.
    /// </summary>
    [HttpGet("{integrationId:guid}/discover")]
    [ProducesResponseType<IReadOnlyList<DiscoveredResourceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Discover(Guid integrationId, CancellationToken ct)
    {
        try
        {
            var services = await pagerDutyDiscovery.ListServicesAsync(integrationId, ct);
            var dtos = services.Select(s => new DiscoveredResourceDto(s.Id, s.Name, s.RoutingKey)).ToList();
            return Ok(dtos);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    /// <summary>
    /// Starts the OAuth authorization-code flow for an integration and returns the provider
    /// authorization URL the admin's browser should be sent to.
    /// </summary>
    [HttpGet("{integrationId:guid}/connect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Connect(Guid integrationId, CancellationToken ct)
    {
        var integration = await integrationRepo.GetByIdAsync(integrationId, ct);
        if (integration is null)
            return NotFound(new { title = "Integration not found.", status = 404 });

        var providerId = ProviderIdFor(integration.Type);
        if (providerId is null)
            return BadRequest(new { title = $"Integration type {integration.Type} does not support OAuth.", status = 400 });

        try
        {
            var url = await oauthClient.BuildAuthorizationUrlAsync(providerId, integrationId, ct);
            return Ok(new { authorizationUrl = url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    /// <summary>
    /// OAuth callback: exchanges the authorization code for tokens and stores them (encrypted)
    /// against the integration the connection was started for.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("callback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Callback([FromBody] OAuthCallbackRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Code) || string.IsNullOrEmpty(request.State))
            return BadRequest(new { title = "code and state are required.", status = 400 });

        try
        {
            var result = await oauthClient.ExchangeCodeAsync(request.Code, request.State, ct);
            await tokenStore.SaveAsync(result.IntegrationId, result.Tokens, ct);
            return Ok(new { integrationId = result.IntegrationId, provider = result.ProviderId, connected = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    /// <summary>
    /// The redirect URL the admin must register in the provider's OAuth app — resolved by the backend
    /// (from the site URL) so it's always exactly what the OAuth flow will send, never guessed by the UI.
    /// </summary>
    [HttpGet("redirect-uri")]
    [ProducesResponseType<OAuthRedirectUriDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RedirectUri(CancellationToken ct)
    {
        var uri = await oauthClient.GetRedirectUriAsync(ct);
        return Ok(new OAuthRedirectUriDto(uri));
    }

    /// <summary>Returns whether an integration currently has a stored OAuth connection.</summary>
    [HttpGet("{integrationId:guid}/status")]
    [ProducesResponseType<OAuthConnectionStatusDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(Guid integrationId, CancellationToken ct)
    {
        var tokens = await tokenStore.GetAsync(integrationId, ct);
        return Ok(new OAuthConnectionStatusDto(
            Connected: tokens is not null,
            ExpiresAt: tokens?.ExpiresAt,
            Scopes: tokens?.Scopes));
    }

    /// <summary>Disconnects an integration by removing its stored OAuth tokens.</summary>
    [HttpPost("{integrationId:guid}/disconnect")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disconnect(Guid integrationId, CancellationToken ct)
    {
        await tokenStore.DeleteAsync(integrationId, ct);
        return NoContent();
    }

    private static string? ProviderIdFor(Domain.Enums.IntegrationType type) => type switch
    {
        Domain.Enums.IntegrationType.PagerDuty => "pagerduty",
        _ => null
    };
}

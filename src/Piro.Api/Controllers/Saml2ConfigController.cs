using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Api.Controllers;

/// <summary>Admin endpoints for managing SAML 2.0 provider configurations. Owner-only.</summary>
[ApiController]
[Route("api/v1/saml/providers")]
[Produces("application/json")]
[Authorize(Roles = "Owner")]
public class Saml2ConfigController(ISaml2Service saml2Service) : ControllerBase
{
    /// <summary>Lists all configured SAML providers (certificate surfaced as a boolean).</summary>
    [HttpGet]
    [ProducesResponseType<List<Saml2ProviderConfigDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await saml2Service.GetAllConfigsAsync(ct));

    /// <summary>Creates or updates a SAML provider configuration.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert([FromBody] UpsertSaml2ProviderRequest request, CancellationToken ct)
    {
        try
        {
            await saml2Service.UpsertConfigAsync(request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    /// <summary>Tests a saved provider's configuration is internally consistent (parseable cert, present endpoints).</summary>
    [HttpPost("test")]
    [ProducesResponseType<object>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Test([FromBody] TestSaml2Request request, CancellationToken ct)
    {
        try
        {
            var ok = await saml2Service.TestProviderAsync(
                request.ProviderId ?? throw new InvalidOperationException("providerId is required."),
                ct);
            return Ok(new { success = ok, message = ok ? "Provider configuration is valid." : "Provider check failed." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Permanently deletes a SAML provider configuration.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await saml2Service.DeleteConfigAsync(id, ct);
        return NoContent();
    }

    /// <summary>Parses an uploaded IdP metadata XML document into the entity ID, SSO URL, and signing certificate.</summary>
    [HttpPost("parse-metadata")]
    [ProducesResponseType<Saml2MetadataResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ParseMetadata([FromBody] ParseSaml2MetadataRequest request)
    {
        try
        {
            return Ok(saml2Service.ParseMetadata(request.MetadataXml));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }
}

public record TestSaml2Request(string? ProviderId);
public record ParseSaml2MetadataRequest(string MetadataXml);

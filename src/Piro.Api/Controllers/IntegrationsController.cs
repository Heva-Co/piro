using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Services;
using Piro.Domain.Attributes;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Api.Controllers;

[Authorize(Roles = "Owner,Admin")]
[ApiController]
[Route("api/v1/integrations")]
[Produces("application/json")]
public class IntegrationsController(IntegrationAppService integrationApp) : ControllerBase
{
    /// <summary>
    /// Returns the manifest (category, direction, capabilities, ConfigJson schema) for every
    /// non-obsolete IntegrationType — see RFC 0003. Reflected from each type's ConfigType, not
    /// hand-authored, so it can't drift from what the code actually deserializes.
    /// </summary>
    [HttpGet("types")]
    [ProducesResponseType<IEnumerable<IntegrationTypeMetaDto>>(StatusCodes.Status200OK)]
    public IActionResult GetTypes()
    {
        var types = Enum.GetValues<IntegrationType>()
            .Select(t => t.ToMetaDto())
            .Where(dto => dto is not null);

        return Ok(types);
    }

    /// <summary>Returns every configured Integration, with secret config fields masked.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<IntegrationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await integrationApp.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<IntegrationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await integrationApp.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<IntegrationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateIntegrationRequest request, CancellationToken ct)
    {
        if (request.Type.IsChannelOnly())
            return BadRequest(new { error = $"Integration type '{request.Type}' does not support global credentials. Configure it directly on the Notification Channel." });

        var created = await integrationApp.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<IntegrationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateIntegrationRequest request, CancellationToken ct) =>
        Ok(await integrationApp.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await integrationApp.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (DomainValidationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}

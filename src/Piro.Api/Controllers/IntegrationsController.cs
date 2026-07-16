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

    [HttpGet("{id:guid}")]
    [ProducesResponseType<IntegrationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
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

    [HttpPut("{id:guid}")]
    [ProducesResponseType<IntegrationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIntegrationRequest request, CancellationToken ct)
    {
        return Ok(await integrationApp.UpdateAsync(id, request, ct));
    }


    /// <summary>Returns the most recent inbound webhook requests for this Integration — RFC 0001 §4.4.</summary>
    [HttpGet("{id:guid}/webhook-logs")]
    [ProducesResponseType<IEnumerable<WebhookRequestLogDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWebhookLogs(Guid id, CancellationToken ct)
    {
        return Ok(await integrationApp.GetWebhookLogsAsync(id, ct));
    }

    /// <summary>
    /// Regenerates this Integration's server-generated fields (e.g. a lost/leaked webhook auth
    /// token) and invalidates the old value immediately. Response's ConfigJson is unmasked, the one
    /// time the new value is visible.
    /// </summary>
    [HttpPost("{id:guid}/regenerate-generated-fields")]
    [ProducesResponseType<IntegrationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegenerateGeneratedFields(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await integrationApp.RegenerateGeneratedFieldsAsync(id, ct));
        }
        catch (DomainValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }


    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
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

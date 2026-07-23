using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Integrations.Actions;
using Piro.Application.Extensions;
using Piro.Application.Services;
using Piro.Contracts;
using Piro.Domain.Exceptions;
using Piro.Integrations.Abstractions;

namespace Piro.Api.Controllers;

[Authorize(Roles = "Owner,Admin")]
[ApiController]
[Route("api/v1/integrations")]
[Produces("application/json")]
public class IntegrationsController(IntegrationAppService integrationApp, IIntegrationRegistry registry) : ControllerBase
{
    /// <summary>
    /// Returns the manifest (category, derived direction, capabilities, ConfigJson schema) for every
    /// registered integration (RFC 0016) — enumerated from the integration registry, reflected from
    /// each integration's ConfigType, so it can't drift from what the code actually deserializes.
    /// </summary>
    [HttpGet("types")]
    [ProducesResponseType<IEnumerable<IntegrationTypeMetaDto>>(StatusCodes.Status200OK)]
    public IActionResult GetTypes()
    {
        var types = registry.All.Select(i => i.ToMetaDto());
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

    /// <summary>
    /// Discovery — which integration action buttons to render for an object of the given context
    /// (RFC 0012 §4.4). One descriptor per (configured integration × ready action). A not-ready action
    /// is absent, never disabled.
    /// </summary>
    [HttpGet("actions")]
    [ProducesResponseType<IReadOnlyList<IntegrationActionDescriptorDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActions(
        [FromQuery] Domain.Enums.ActionContext context, CancellationToken ct) =>
        Ok(await integrationApp.GetActionsAsync(context, ct));

    /// <summary>
    /// Resolves the runtime options for a dynamic-options field (RFC 0012) — e.g. the connected Jira
    /// account's projects, or the issue types for a chosen project (pass its key as <paramref name="dependsOn"/>).
    /// </summary>
    [HttpGet("{id:guid}/options/{sourceKey}")]
    [ProducesResponseType<IReadOnlyList<OptionItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFieldOptions(
        Guid id, string sourceKey, [FromQuery] string? dependsOn, CancellationToken ct) =>
        Ok(await integrationApp.GetFieldOptionsAsync(id, sourceKey, dependsOn, ct));

    /// <summary>Pre-fills an action's input dialog for a specific target (RFC 0012 §4.6).</summary>
    [HttpGet("{id:guid}/actions/{actionId}/draft")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActionDraft(
        Guid id, string actionId,
        [FromQuery] Domain.Enums.ActionContext context, [FromQuery] int targetId, CancellationToken ct) =>
        Ok(await integrationApp.BuildActionDraftAsync(id, actionId, context, targetId, ct));

    /// <summary>Executes a user-initiated integration action and returns the external reference it created (RFC 0012 §4.4).</summary>
    [HttpPost("{id:guid}/actions/{actionId}/execute")]
    [ProducesResponseType<IntegrationActionResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExecuteAction(
        Guid id, string actionId, [FromBody] ExecuteIntegrationActionRequest request, CancellationToken ct) =>
        Ok(await integrationApp.ExecuteActionAsync(id, actionId, request, ct));

    /// <summary>
    /// Returns the outbound external references (e.g. a linked Jira ticket) that integration actions
    /// have created for a local object — Alert/Incident/Maintenance (RFC 0012 §4.5). The detail page
    /// renders these as "🔗 OPS-123" links alongside the action buttons.
    /// </summary>
    [HttpGet("references")]
    [ProducesResponseType<IReadOnlyList<ExternalReferenceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferences(
        [FromQuery] Domain.Enums.ActionContext context, [FromQuery] int targetId, CancellationToken ct) =>
        Ok(await integrationApp.GetReferencesAsync(context, targetId, ct));

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

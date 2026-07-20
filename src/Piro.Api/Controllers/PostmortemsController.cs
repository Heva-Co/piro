using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>
/// CRUD and lifecycle management for postmortem reports (RFC 0005). A free-standing resource that
/// references incidents (N:M) — hence a top-level route rather than a sub-resource of incidents.
/// </summary>
[ApiController]
[Route("api/v1/postmortems")]
[Produces("application/json")]
[Authorize(Roles = "Owner,Admin,Member")]
public class PostmortemsController(PostmortemAppService postmortemService) : ControllerBase
{
    /// <summary>Lists all postmortems, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<PostmortemListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await postmortemService.GetAllAsync(ct));

    /// <summary>Returns the analysis template (active field definitions) — for rendering an empty editor.</summary>
    [HttpGet("field-definitions")]
    [ProducesResponseType<IEnumerable<PostmortemFieldDefinitionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFieldDefinitions(CancellationToken ct) =>
        Ok(await postmortemService.GetFieldDefinitionsAsync(ct));

    /// <summary>Returns a single postmortem with its analysis fields and derived timeline.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<PostmortemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await postmortemService.GetByIdAsync(id, ct));

    /// <summary>Creates a Draft report and seeds an empty value per active field definition.</summary>
    [HttpPost]
    [ProducesResponseType<PostmortemDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreatePostmortemRequest request, CancellationToken ct)
    {
        var created = await postmortemService.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Updates report metadata and/or its analysis field values.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<PostmortemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePostmortemRequest request, CancellationToken ct) =>
        Ok(await postmortemService.UpdateAsync(id, request, ct));

    /// <summary>Publishes a Draft report (internal-only in Phase 1).</summary>
    [HttpPost("{id:int}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(int id, CancellationToken ct)
    {
        await postmortemService.PublishAsync(id, ct);
        return NoContent();
    }

    /// <summary>Reverts a Published report back to Draft.</summary>
    [HttpPost("{id:int}/unpublish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(int id, CancellationToken ct)
    {
        await postmortemService.UnpublishAsync(id, ct);
        return NoContent();
    }

    /// <summary>Deletes a postmortem and its analysis content (referenced incidents are untouched).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await postmortemService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Links an incident to a postmortem (N:M "data source").</summary>
    [HttpPost("{id:int}/incidents")]
    [ProducesResponseType<PostmortemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LinkIncident(int id, [FromBody] LinkIncidentRequest request, CancellationToken ct) =>
        Ok(await postmortemService.LinkIncidentAsync(id, request, ct));

    /// <summary>Removes an incident link from a postmortem.</summary>
    [HttpDelete("{id:int}/incidents/{incidentId:int}")]
    [ProducesResponseType<PostmortemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkIncident(int id, int incidentId, CancellationToken ct) =>
        Ok(await postmortemService.UnlinkIncidentAsync(id, incidentId, ct));
}

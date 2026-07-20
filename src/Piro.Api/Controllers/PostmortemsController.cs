using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>
/// CRUD and lifecycle management for postmortem reports (RFC 0005). A free-standing resource that
/// references incidents (N:M), hence a top-level route rather than a sub-resource of incidents.
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

    /// <summary>
    /// Returns the analysis template. By default only active definitions (for rendering an empty editor);
    /// pass <paramref name="includeInactive"/> = true to include deactivated ones (template management).
    /// </summary>
    [HttpGet("field-definitions")]
    [ProducesResponseType<IEnumerable<PostmortemFieldDefinitionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFieldDefinitions([FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        Ok(includeInactive
            ? await postmortemService.GetAllFieldDefinitionsAsync(ct)
            : await postmortemService.GetFieldDefinitionsAsync(ct));

    /// <summary>Creates a custom analysis field. Restricted to Owner/Admin (RFC 0005 Phase 3a).</summary>
    [HttpPost("field-definitions")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<PostmortemFieldDefinitionDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateFieldDefinition([FromBody] CreateFieldDefinitionRequest request, CancellationToken ct)
    {
        var created = await postmortemService.CreateFieldDefinitionAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Edits a field definition (heading/help/active for any; type only for custom). Owner/Admin only.</summary>
    [HttpPut("field-definitions/{defId:int}")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<PostmortemFieldDefinitionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFieldDefinition(int defId, [FromBody] UpdateFieldDefinitionRequest request, CancellationToken ct) =>
        Ok(await postmortemService.UpdateFieldDefinitionAsync(defId, request, ct));

    /// <summary>Reorders the analysis template. Owner/Admin only.</summary>
    [HttpPut("field-definitions/order")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<IEnumerable<PostmortemFieldDefinitionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReorderFieldDefinitions([FromBody] ReorderFieldDefinitionsRequest request, CancellationToken ct) =>
        Ok(await postmortemService.ReorderFieldDefinitionsAsync(request, ct));

    /// <summary>Deletes a custom field (or deactivates it if in use). System fields can't be deleted. Owner/Admin only.</summary>
    [HttpDelete("field-definitions/{defId:int}")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFieldDefinition(int defId, CancellationToken ct)
    {
        await postmortemService.DeleteFieldDefinitionAsync(defId, ct);
        return NoContent();
    }

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

    /// <summary>Downloads the finalized report as a PDF. Only available once the report is Published.</summary>
    [HttpGet("{id:int}/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(int id, CancellationToken ct)
    {
        var (bytes, fileName) = await postmortemService.GeneratePdfAsync(id, ct);
        return File(bytes, "application/pdf", fileName);
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

    /// <summary>Suggests incidents to link, from those overlapping the report's impact window.</summary>
    [HttpGet("{id:int}/incident-suggestions")]
    [ProducesResponseType<IEnumerable<PostmortemIncidentSuggestionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIncidentSuggestions(int id, CancellationToken ct) =>
        Ok(await postmortemService.GetIncidentSuggestionsAsync(id, ct));

    /// <summary>Adds an author annotation to the report's timeline.</summary>
    [HttpPost("{id:int}/timeline")]
    [ProducesResponseType<PostmortemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTimelineEntry(int id, [FromBody] CreateTimelineEntryRequest request, CancellationToken ct)
    {
        var author = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Unknown";
        return Ok(await postmortemService.AddTimelineEntryAsync(id, request, author, ct));
    }

    /// <summary>Edits an existing timeline annotation.</summary>
    [HttpPut("{id:int}/timeline/{entryId:int}")]
    [ProducesResponseType<PostmortemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTimelineEntry(int id, int entryId, [FromBody] UpdateTimelineEntryRequest request, CancellationToken ct) =>
        Ok(await postmortemService.UpdateTimelineEntryAsync(id, entryId, request, ct));

    /// <summary>Deletes a timeline annotation.</summary>
    [HttpDelete("{id:int}/timeline/{entryId:int}")]
    [ProducesResponseType<PostmortemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTimelineEntry(int id, int entryId, CancellationToken ct) =>
        Ok(await postmortemService.DeleteTimelineEntryAsync(id, entryId, ct));
}

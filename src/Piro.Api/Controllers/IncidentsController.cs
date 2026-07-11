using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>CRUD and lifecycle management for incidents.</summary>
[ApiController]
[Route("api/v1/incidents")]
[Produces("application/json")]
[Authorize(Roles = "Owner,Admin,Member")]
public class IncidentsController(IncidentAppService incidentService) : ControllerBase
{
    /// <summary>
    /// Returns incidents filtered by <paramref name="filter"/>:
    /// "active" (default) = non-resolved, "all" = everything, "resolved" = only resolved,
    /// or a specific state name: "investigating", "identified", "monitoring".
    /// Requires authentication — returns all incidents regardless of publish state.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<IncidentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string filter = "active", CancellationToken ct = default) =>
        Ok(await incidentService.GetAllAsync(filter, ct));

    /// <summary>Returns a single incident by ID with the full admin view (internal fields included).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await incidentService.GetByIdAsync(id, ct));

    /// <summary>
    /// Returns a paginated page of an incident's full timeline (all events, any visibility), most
    /// recent first — used for the admin "view full timeline" infinite-scroll flow.
    /// </summary>
    [HttpGet("{id:int}/timeline")]
    [ProducesResponseType<IncidentTimelinePageDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimeline(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await incidentService.GetTimelineAsync(id, page, pageSize, publicOnly: false, ct));

    /// <summary>Creates a new incident and optionally links affected services.</summary>
    [HttpPost]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateIncidentRequest request, CancellationToken ct)
    {
        var created = await incidentService.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Updates incident metadata or advances its investigation state.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateIncidentRequest request, CancellationToken ct) =>
        Ok(await incidentService.UpdateAsync(id, request, ct));

    /// <summary>Posts an update on an incident and optionally advances its status.</summary>
    [HttpPost("{id:int}/updates")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTimelineComment(int id, [FromBody] AddTimelineCommentRequest request, CancellationToken ct)
    {
        await incidentService.AddTimelineCommentAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>Updates the text or visibility of an existing update.</summary>
    [HttpPut("{id:int}/updates/{eventId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTimelineComment(int id, int eventId, [FromBody] UpdateTimelineCommentRequest request, CancellationToken ct)
    {
        await incidentService.UpdateTimelineCommentAsync(id, eventId, request, ct);
        return NoContent();
    }

    /// <summary>Deletes a single update from an incident's timeline.</summary>
    [HttpDelete("{id:int}/updates/{eventId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTimelineComment(int id, int eventId, CancellationToken ct)
    {
        await incidentService.DeleteTimelineCommentAsync(id, eventId, ct);
        return NoContent();
    }

    /// <summary>Acknowledges an incident, marking the current user as the responder.</summary>
    [HttpPost("{id:int}/acknowledge")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Acknowledge(int id, CancellationToken ct)
    {
        var name = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Unknown";
        return Ok(await incidentService.AcknowledgeAsync(id, name, ct));
    }

    /// <summary>Replaces the full set of affected services for an incident in one call.</summary>
    [HttpPut("{id:int}/services")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetServices(int id, [FromBody] SetIncidentServicesRequest request, CancellationToken ct) =>
        Ok(await incidentService.SetServicesAsync(id, request, ct));

    /// <summary>Links an affected service to an incident.</summary>
    [HttpPost("{id:int}/services")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddService(int id, [FromBody] AddIncidentServiceRequest request, CancellationToken ct) =>
        Ok(await incidentService.AddServiceAsync(id, request, ct));

    /// <summary>Removes a service link from an incident.</summary>
    [HttpDelete("{id:int}/services/{serviceSlug}")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveService(int id, string serviceSlug, CancellationToken ct) =>
        Ok(await incidentService.RemoveServiceAsync(id, serviceSlug, ct));

    /// <summary>Deletes an incident and all its timeline events.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await incidentService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Publishes an incident to the status page. Always a manual, explicit action.</summary>
    [HttpPost("{id:int}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(int id, CancellationToken ct)
    {
        await incidentService.PublishAsync(id, ct);
        return NoContent();
    }

    /// <summary>Reverts a published incident back to private, hiding it (and its public comments) from the status page.</summary>
    [HttpPost("{id:int}/unpublish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(int id, CancellationToken ct)
    {
        await incidentService.UnpublishAsync(id, ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>CRUD and lifecycle management for incidents.</summary>
[ApiController]
[Route("api/v1/incidents")]
[Produces("application/json")]
[Authorize]
public class IncidentsController(IncidentAppService incidentService) : ControllerBase
{
    /// <summary>Returns all active incidents. Pass <c>?includeResolved=true</c> to include resolved ones.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IEnumerable<IncidentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeResolved = false, CancellationToken ct = default) =>
        Ok(await incidentService.GetAllAsync(includeResolved, ct));

    /// <summary>Returns a single incident by ID.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await incidentService.GetByIdAsync(id, ct));

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

    /// <summary>Posts a status update comment on an incident and optionally advances its state.</summary>
    [HttpPost("{id:int}/comments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentRequest request, CancellationToken ct)
    {
        await incidentService.AddCommentAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>Updates the text or state of an existing comment.</summary>
    [HttpPut("{id:int}/comments/{commentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateComment(int id, int commentId, [FromBody] UpdateCommentRequest request, CancellationToken ct)
    {
        await incidentService.UpdateCommentAsync(id, commentId, request, ct);
        return NoContent();
    }

    /// <summary>Deletes a single comment from an incident.</summary>
    [HttpDelete("{id:int}/comments/{commentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComment(int id, int commentId, CancellationToken ct)
    {
        await incidentService.DeleteCommentAsync(id, commentId, ct);
        return NoContent();
    }

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

    /// <summary>Deletes an incident and all its comments.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await incidentService.DeleteAsync(id, ct);
        return NoContent();
    }
}

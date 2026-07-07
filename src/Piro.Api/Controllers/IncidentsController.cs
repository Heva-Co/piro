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
[Authorize]
public class IncidentsController(IncidentAppService incidentService, IIncidentPublishScheduler publishScheduler) : ControllerBase
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

    /// <summary>
    /// Returns published, non-merged incidents for the public status page.
    /// Pass <c>includeResolved=true</c> to include incident history.
    /// </summary>
    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType<IEnumerable<IncidentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublic([FromQuery] bool includeResolved = false, CancellationToken ct = default) =>
        Ok(await incidentService.GetAllPublicAsync(includeResolved, ct));

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

    /// <summary>Deletes an incident and all its comments.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await incidentService.DeleteAsync(id, ct);
        await publishScheduler.CancelAsync(id, ct);
        return NoContent();
    }

    /// <summary>Immediately publishes a draft incident to the status page, cancelling any pending auto-publish timer.</summary>
    [HttpPost("{id:int}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(int id, CancellationToken ct)
    {
        await incidentService.PublishAsync(id, ct);
        await publishScheduler.CancelAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Extends the auto-publish timer by the specified number of minutes.
    /// If no timer is pending, schedules a new one from now.
    /// </summary>
    [HttpPost("{id:int}/publish/delay")]
    [ProducesResponseType<PublishScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DelayPublish(int id, [FromBody] DelayPublishRequest request, CancellationToken ct)
    {
        await incidentService.GetByIdAsync(id, ct); // throws 404 if not found
        await publishScheduler.ExtendAsync(id, request.AdditionalMinutes, ct);
        var scheduledAt = await publishScheduler.GetScheduledTimeAsync(id, ct);
        return Ok(new PublishScheduleDto(scheduledAt));
    }

    /// <summary>Cancels the auto-publish timer, keeping the incident as a draft indefinitely.</summary>
    [HttpDelete("{id:int}/publish/schedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CancelPublish(int id, CancellationToken ct)
    {
        await publishScheduler.CancelAsync(id, ct);
        return NoContent();
    }

    /// <summary>Returns when the incident is scheduled to be auto-published, or null if no timer is set.</summary>
    [HttpGet("{id:int}/publish/schedule")]
    [ProducesResponseType<PublishScheduleDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublishSchedule(int id, CancellationToken ct)
    {
        var scheduledAt = await publishScheduler.GetScheduledTimeAsync(id, ct);
        return Ok(new PublishScheduleDto(scheduledAt));
    }
}

public record DelayPublishRequest(int AdditionalMinutes);
public record PublishScheduleDto(DateTimeOffset? ScheduledAt);

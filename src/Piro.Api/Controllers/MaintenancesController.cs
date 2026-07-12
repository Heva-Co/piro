using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>CRUD for maintenance windows and their materialized event occurrences.</summary>
[ApiController]
[Route("api/v1/maintenances")]
[Produces("application/json")]
[Authorize]
public class MaintenancesController(MaintenanceAppService maintenanceService) : ControllerBase
{
    /// <summary>Returns all maintenance windows.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<MaintenanceListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await maintenanceService.GetAllAsync(ct));

    /// <summary>Returns a single maintenance window by ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<MaintenanceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await maintenanceService.GetByIdAsync(id, ct));

    /// <summary>
    /// Creates a maintenance window.
    /// <c>RRule</c> must be a valid iCalendar RRULE string (e.g. <c>FREQ=WEEKLY;BYDAY=MO</c>).
    /// For a one-time window use <c>FREQ=DAILY;COUNT=1</c>.
    /// Events are materialized automatically for the next 90 days.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<MaintenanceDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateMaintenanceRequest request, CancellationToken ct)
    {
        var created = await maintenanceService.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Updates a maintenance window. Changing the RRULE re-materializes all future events.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<MaintenanceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMaintenanceRequest request, CancellationToken ct) =>
        Ok(await maintenanceService.UpdateAsync(id, request, ct));

    /// <summary>Cancels a maintenance window and removes all future scheduled events.</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await maintenanceService.CancelAsync(id, ct);
        return NoContent();
    }

    /// <summary>Deletes a maintenance window and all its events.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await maintenanceService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Cancels a single occurrence of a recurring maintenance, leaving the maintenance and its other events untouched.</summary>
    [HttpPost("{id:int}/events/{eventId:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelEvent(int id, int eventId, CancellationToken ct)
    {
        await maintenanceService.CancelEventAsync(id, eventId, ct);
        return NoContent();
    }
}

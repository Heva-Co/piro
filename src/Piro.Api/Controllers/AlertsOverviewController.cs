using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>Global alerts endpoint — returns Alert instances across all checks/services.</summary>
[ApiController]
[Route("api/v1/alerts")]
[Authorize]
[Produces("application/json")]
public class AlertsOverviewController(AlertAppService alertApp) : ControllerBase
{
    /// <summary>
    /// Returns a paginated list of Alerts (active and historical). Active alerts always sort
    /// before resolved ones, then most-recently-fired first. Optionally filter by FiredAt range.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<AlertPageDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default) =>
        Ok(await alertApp.GetPagedAsync(new AlertQueryParams(from, to, page, pageSize, activeOnly), ct));

    /// <summary>Returns the full detail of a single Alert, including the AlertConfig criteria that fired it.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<AlertDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await alertApp.GetByIdAsync(id, ct));

    /// <summary>Returns all open incidents — for the "attach to incident" picker on an alert.</summary>
    [HttpGet("open-incidents")]
    [ProducesResponseType<IEnumerable<IncidentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOpenIncidents(CancellationToken ct) =>
        Ok(await alertApp.GetOpenIncidentsAsync(ct));

    /// <summary>Returns the full on-call delivery history for this alert's escalation, most recent first.</summary>
    [HttpGet("{id:int}/escalation-logs")]
    [ProducesResponseType<IEnumerable<EscalationDeliveryLogDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEscalationLogs(int id, CancellationToken ct) =>
        Ok(await alertApp.GetEscalationLogsAsync(id, ct));

    /// <summary>
    /// Links this alert to an incident — creates a new one if <c>incidentId</c> is omitted, or
    /// attaches to the given existing incident. Always an explicit, manual action.
    /// </summary>
    [HttpPost("{id:int}/incident")]
    [ProducesResponseType<AlertDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkToIncident(int id, [FromBody] LinkAlertToIncidentRequest request, CancellationToken ct) =>
        Ok(await alertApp.LinkToIncidentAsync(id, request, ct));

    /// <summary>Acknowledges an alert, pausing its on-call escalation.</summary>
    [HttpPost("{id:int}/acknowledge")]
    [ProducesResponseType<AlertDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Acknowledge(int id, CancellationToken ct)
    {
        var name = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Unknown";
        return Ok(await alertApp.AcknowledgeAsync(id, name, ct));
    }
}

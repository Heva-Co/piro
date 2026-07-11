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
        CancellationToken ct = default) =>
        Ok(await alertApp.GetPagedAsync(new AlertQueryParams(from, to, page, pageSize), ct));

    /// <summary>Returns the full detail of a single Alert, including the AlertConfig criteria that fired it.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<AlertDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await alertApp.GetByIdAsync(id, ct));
}

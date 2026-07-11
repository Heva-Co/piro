using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>Aggregated incident-response metrics for the admin dashboard.</summary>
[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
[Produces("application/json")]
public class DashboardController(DashboardAppService dashboardApp) : ControllerBase
{
    /// <summary>
    /// Returns MTTA/MTTR/MTTD, alert noise ratio, and incident/alert breakdowns for a date range.
    /// Defaults to the current calendar month (UTC) when <c>from</c>/<c>to</c> are omitted.
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType<DashboardMetricsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        Ok(await dashboardApp.GetMetricsAsync(from, to, ct));
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>Global cross-entity search for the admin panel (Cmd+K).</summary>
[ApiController]
[Route("api/v1/search")]
[Produces("application/json")]
[Authorize]
public class SearchController(SearchAppService searchService) : ControllerBase
{
    /// <summary>
    /// Searches Services, Checks, Alerts, Incidents, Maintenances, On-Call Schedules, and Escalation
    /// Policies by name/title/slug. Owner/Admin callers additionally get Users and API Keys in the results.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<List<SearchResultDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        var canSeeUsersAndApiKeys = User.IsInRole("Owner") || User.IsInRole("Admin");
        var results = await searchService.SearchAsync(q, canSeeUsersAndApiKeys, ct);
        return Ok(results);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>Global checks endpoint — returns all checks across all services.</summary>
[ApiController]
[Route("api/v1/checks")]
[Authorize]
[Produces("application/json")]
public class ChecksOverviewController(CheckAppService checkApp) : ControllerBase
{
    /// <summary>Returns all checks across all services with their parent service info.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<CheckSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await checkApp.GetAllAsync(ct));
}

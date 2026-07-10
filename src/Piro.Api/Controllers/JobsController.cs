using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Api.Controllers;

/// <summary>Exposes scheduling status of all registered background jobs (Quartz triggers).</summary>
[ApiController]
[Route("api/v1/jobs")]
[Authorize]
[Produces("application/json")]
public class JobsController(IJobStatusService jobStatusService) : ControllerBase
{
    /// <summary>Returns next/previous fire time and state for every scheduled job.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<JobStatusDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await jobStatusService.GetAllAsync(ct));
}

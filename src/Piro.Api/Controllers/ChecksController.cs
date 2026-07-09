using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;
using Piro.Domain.Exceptions;

namespace Piro.Api.Controllers;

/// <summary>Manages check definitions within a service.</summary>
[ApiController]
[Route("api/v1/services/{serviceSlug}/checks")]
[Produces("application/json")]
public class ChecksController(CheckAppService checkApp, CheckRunnerService checkRunner) : ControllerBase
{
    /// <summary>Returns all checks for the given service.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<CheckDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(string serviceSlug, CancellationToken ct) =>
        Ok(await checkApp.GetByServiceSlugAsync(serviceSlug, ct));

    /// <summary>Returns a single check by its slug.</summary>
    [HttpGet("{checkSlug}")]
    [ProducesResponseType<CheckDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string serviceSlug, string checkSlug, CancellationToken ct) =>
        Ok(await checkApp.GetBySlugAsync(serviceSlug, checkSlug, ct));

    /// <summary>Creates a new check within the service.</summary>
    [HttpPost]
    [ProducesResponseType<CheckDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(string serviceSlug, [FromBody] CreateCheckRequest request, CancellationToken ct)
    {
        var created = await checkApp.CreateAsync(serviceSlug, request, ct);
        return CreatedAtAction(nameof(GetBySlug), new { serviceSlug, checkSlug = created.Slug }, created);
    }

    /// <summary>Updates an existing check. Only provided fields are changed.</summary>
    [HttpPut("{checkSlug}")]
    [ProducesResponseType<CheckDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string serviceSlug, string checkSlug, [FromBody] UpdateCheckRequest request, CancellationToken ct) =>
        Ok(await checkApp.UpdateAsync(serviceSlug, checkSlug, request, ct));

    /// <summary>Deletes a check.</summary>
    [HttpDelete("{checkSlug}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string serviceSlug, string checkSlug, CancellationToken ct)
    {
        await checkApp.DeleteAsync(serviceSlug, checkSlug, ct);
        return NoContent();
    }

    /// <summary>Returns execution log entries for a check. Filter by region and/or ISO 8601 time range.</summary>
    [HttpGet("{checkSlug}/logs")]
    [ProducesResponseType<IEnumerable<CheckDataPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogs(string serviceSlug, string checkSlug,
        [FromQuery] int limit = 20, [FromQuery] string? region = null,
        [FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null,
        CancellationToken ct = default) =>
        Ok(await checkApp.GetRecentLogsAsync(serviceSlug, checkSlug, limit, region, from, to, ct));

    /// <summary>Returns daily up/down/degraded counts per region for the last N days.</summary>
    [HttpGet("{checkSlug}/history")]
    [ProducesResponseType<IEnumerable<CheckDailyStatsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(string serviceSlug, string checkSlug,
        [FromQuery] int days = 14, CancellationToken ct = default) =>
        Ok(await checkApp.GetDailyStatsAsync(serviceSlug, checkSlug, days, ct));

    /// <summary>Manually triggers an immediate check execution. Useful for testing and on-demand refresh.</summary>
    [HttpPost("{checkSlug}/run")]
    [ProducesResponseType<CheckDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Run(string serviceSlug, string checkSlug, CancellationToken ct)
    {
        var check = await checkApp.GetBySlugAsync(serviceSlug, checkSlug, ct);
        await checkRunner.RunAsync(check.Id, ct);
        return Ok(await checkApp.GetBySlugAsync(serviceSlug, checkSlug, ct));
    }
}

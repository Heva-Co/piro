using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Services;

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
        var ran = await checkRunner.RunAsync(check.Id, ct);
        return Ok(ran is not null ? ran.ToDto() : check);
    }

    /// <summary>
    /// Dry-runs a testable check (the Script check) in debug mode: executes the script against the live
    /// target, captures console.log, and returns the raw verdict WITHOUT persisting a datapoint or firing
    /// an alert (RFC 0010 §4.8). The optional body carries candidate config so the operator can test
    /// unsaved edits; an empty body tests the persisted config.
    /// </summary>
    [HttpPost("{checkSlug}/test")]
    [ProducesResponseType<ScriptTestResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Test(string serviceSlug, string checkSlug, [FromBody] TestCheckRequest? request, CancellationToken ct) =>
        Ok(await checkApp.TestAsync(serviceSlug, checkSlug, request?.TypeDataJson, ct));

    /// <summary>
    /// Inbound-token info for a check that receives inbound requests (RFC 0013): the masked token,
    /// last-used time, and the base inbound URL. The raw token is not returned here — only on rotate.
    /// </summary>
    [HttpGet("{checkSlug}/inbound-token")]
    [ProducesResponseType<CheckInboundTokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InboundTokenInfo(string serviceSlug, string checkSlug, CancellationToken ct)
    {
        var (checkId, token) = await checkApp.GetInboundTokenAsync(serviceSlug, checkSlug, ct);
        // The masked token can't reconstruct the raw URL, so this is the base endpoint (no token); the
        // operator gets the tokenized URL from rotate. Show the masked token + last-used for reference.
        return Ok(new CheckInboundTokenDto(InboundUrlBase(checkId), token?.MaskedToken, token?.LastUsedAt));
    }

    /// <summary>Rotates a check's inbound token (RFC 0013) and returns the new raw token + full inbound URL, shown once.</summary>
    [HttpPost("{checkSlug}/inbound-token/rotate")]
    [ProducesResponseType<CheckInboundTokenRotateResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RotateInboundToken(string serviceSlug, string checkSlug, CancellationToken ct)
    {
        var (checkId, rawToken) = await checkApp.RotateInboundTokenAsync(serviceSlug, checkSlug, ct);
        return Ok(new CheckInboundTokenRotateResultDto(rawToken, $"{InboundUrlBase(checkId)}?token={rawToken}"));
    }

    private string InboundUrlBase(int checkId) =>
        $"{Request.Scheme}://{Request.Host}/api/v1/checks/{checkId}/inbound";
}

/// <summary>Body for the check Test endpoint — the candidate config to test, or null to use the persisted one.</summary>
public record TestCheckRequest(string? TypeDataJson);

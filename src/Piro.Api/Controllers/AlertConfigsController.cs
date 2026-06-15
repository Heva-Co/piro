using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>Manages alert configurations for a check.</summary>
[Authorize]
[ApiController]
[Route("api/v1/services/{serviceSlug}/checks/{checkSlug}/alert-configs")]
[Produces("application/json")]
public class AlertConfigsController(AlertConfigAppService alertConfigApp) : ControllerBase
{
    /// <summary>Returns all alert configs for the given check.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<AlertConfigDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(string serviceSlug, string checkSlug, CancellationToken ct) =>
        Ok(await alertConfigApp.GetByCheckAsync(serviceSlug, checkSlug, ct));

    /// <summary>Returns a single alert config by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<AlertConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string serviceSlug, string checkSlug, int id, CancellationToken ct) =>
        Ok(await alertConfigApp.GetByIdAsync(serviceSlug, checkSlug, id, ct));

    /// <summary>Creates a new alert config on the check.</summary>
    [HttpPost]
    [ProducesResponseType<AlertConfigDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        string serviceSlug, string checkSlug,
        [FromBody] CreateAlertConfigRequest request, CancellationToken ct)
    {
        var created = await alertConfigApp.CreateAsync(serviceSlug, checkSlug, request, ct);
        return CreatedAtAction(nameof(GetById), new { serviceSlug, checkSlug, id = created.Id }, created);
    }

    /// <summary>Updates an existing alert config.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<AlertConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string serviceSlug, string checkSlug, int id,
        [FromBody] UpdateAlertConfigRequest request, CancellationToken ct) =>
        Ok(await alertConfigApp.UpdateAsync(serviceSlug, checkSlug, id, request, ct));

    /// <summary>Deletes an alert config.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string serviceSlug, string checkSlug, int id, CancellationToken ct)
    {
        await alertConfigApp.DeleteAsync(serviceSlug, checkSlug, id, ct);
        return NoContent();
    }
}

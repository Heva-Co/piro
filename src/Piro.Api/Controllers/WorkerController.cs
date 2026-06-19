using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Infrastructure.Workers;

namespace Piro.Api.Controllers;

/// <summary>Manages remote check worker registrations.</summary>
[ApiController]
[Route("api/v1/workers")]
[Produces("application/json")]
[Authorize]
public class WorkerController(
    WorkerAppService workerApp,
    ISiteConfigRepository siteConfig,
    ApiWorkerHostedService builtinWorker) : ControllerBase
{
    /// <summary>Returns all registered workers with their live connection state.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<WorkerDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await workerApp.GetAllAsync(ct));

    /// <summary>
    /// Registers a new remote worker and returns its token.
    /// The token is shown exactly once — store it immediately as PIRO_WORKER_TOKEN.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<CreateWorkerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateWorkerRequest request, CancellationToken ct)
    {
        var response = await workerApp.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), response);
    }

    /// <summary>
    /// Enables or disables the built-in API worker at runtime without restarting the application.
    /// The setting is also persisted to the database so it survives restarts.
    /// </summary>
    [HttpPost("builtin/toggle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ToggleBuiltin([FromBody] ToggleBuiltinRequest request, CancellationToken ct)
    {
        await siteConfig.SetAsync(
            "worker:builtin_disabled",
            request.Disabled ? "true" : null,
            ct);

        if (request.Disabled)
            builtinWorker.Disable();
        else
            builtinWorker.Enable();

        return Ok(new { enabled = !request.Disabled });
    }

    /// <summary>Updates mutable fields (region) of a worker registration.</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkerRequest request, CancellationToken ct)
    {
        await workerApp.UpdateAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>Deletes a worker registration and invalidates its token.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await workerApp.DeleteAsync(id, ct);
        return NoContent();
    }
}

public record ToggleBuiltinRequest(bool Disabled);

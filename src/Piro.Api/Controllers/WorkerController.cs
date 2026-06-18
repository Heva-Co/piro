using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>Manages remote check worker registrations.</summary>
[ApiController]
[Route("api/v1/workers")]
[Produces("application/json")]
[Authorize]
public class WorkerController(
    WorkerAppService workerApp,
    ISiteConfigRepository siteConfig,
    IHostApplicationLifetime appLifetime) : ControllerBase
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
    /// Enables or disables the built-in API worker. Persists the setting and triggers a graceful
    /// application restart so the change takes effect (requires a restart policy in the host).
    /// </summary>
    [HttpPost("builtin/toggle")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ToggleBuiltin([FromBody] ToggleBuiltinRequest request, CancellationToken ct)
    {
        await siteConfig.SetAsync(
            "worker:builtin_disabled",
            request.Disabled ? "true" : null,
            ct);

        // Trigger graceful shutdown — host (Docker/systemd) will restart the process
        _ = Task.Run(async () =>
        {
            await Task.Delay(500); // give the response time to flush
            appLifetime.StopApplication();
        });

        return Accepted(new { message = "Setting saved. Application is restarting…" });
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

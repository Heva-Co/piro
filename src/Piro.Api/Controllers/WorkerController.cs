using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>Manages remote check worker registrations.</summary>
[ApiController]
[Route("api/v1/workers")]
[Produces("application/json")]
[Authorize]
public class WorkerController(WorkerAppService workerApp) : ControllerBase
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

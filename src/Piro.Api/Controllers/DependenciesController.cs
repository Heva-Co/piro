using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>Manages the dependency graph edges for a service.</summary>
[ApiController]
[Route("api/v1/services/{serviceSlug}/dependencies")]
[Produces("application/json")]
public class DependenciesController(DependencyService dependencyService) : ControllerBase
{
    /// <summary>Returns all services that the given service depends on.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<DependencyDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(string serviceSlug, CancellationToken ct) =>
        Ok(await dependencyService.GetByServiceSlugAsync(serviceSlug, ct));

    /// <summary>Declares a new dependency. Rejects the edge if it would create a cycle.</summary>
    [HttpPost]
    [ProducesResponseType<DependencyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Add(string serviceSlug, [FromBody] AddDependencyRequest request, CancellationToken ct)
    {
        var created = await dependencyService.AddAsync(serviceSlug, request, ct);
        return CreatedAtAction(nameof(GetAll), new { serviceSlug }, created);
    }

    /// <summary>Removes a declared dependency.</summary>
    [HttpDelete("{dependsOnSlug}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(string serviceSlug, string dependsOnSlug, CancellationToken ct)
    {
        await dependencyService.RemoveAsync(serviceSlug, dependsOnSlug, ct);
        return NoContent();
    }
}

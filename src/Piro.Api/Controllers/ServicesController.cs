using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>Manages service definitions and their configuration.</summary>
[ApiController]
[Route("api/v1/services")]
[Produces("application/json")]
public class ServicesController(ServiceAppService serviceApp) : ControllerBase
{
    /// <summary>Returns all services ordered by display_order.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<ServiceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await serviceApp.GetAllAsync(ct));

    /// <summary>Returns a single service by its slug.</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType<ServiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct) =>
        Ok(await serviceApp.GetBySlugAsync(slug, ct));

    /// <summary>Creates a new service.</summary>
    [HttpPost]
    [ProducesResponseType<ServiceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequest request, CancellationToken ct)
    {
        var created = await serviceApp.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetBySlug), new { slug = created.Slug }, created);
    }

    /// <summary>Updates an existing service. Only provided fields are changed.</summary>
    [HttpPut("{slug}")]
    [ProducesResponseType<ServiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string slug, [FromBody] UpdateServiceRequest request, CancellationToken ct) =>
        Ok(await serviceApp.UpdateAsync(slug, request, ct));

    /// <summary>Deletes a service and all its checks.</summary>
    [HttpDelete("{slug}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string slug, CancellationToken ct)
    {
        await serviceApp.DeleteAsync(slug, ct);
        return NoContent();
    }
}

using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Services;
using Piro.Domain.Enums;

namespace Piro.Api.Controllers;

/// <summary>Manages service definitions and their configuration.</summary>
[ApiController]
[Route("api/v1/services")]
[Produces("application/json")]
public class ServicesController(
    ServiceAppService serviceApp,
    IServiceRepository serviceRepo,
    Channel<CheckStatusChangedEvent> statusChannel) : ControllerBase
{
    /// <summary>Returns a paginated list of services ordered by display_order, optionally filtered by name/slug search.</summary>
    [HttpGet]
    [ProducesResponseType<PaginatedResponse<ServiceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        return Ok(await serviceApp.GetPagedAsync(new ServiceQueryParams(page, pageSize, search), ct));
    }

    /// <summary>Returns a single service by its slug.</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType<ServiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        return Ok(await serviceApp.GetBySlugAsync(slug, ct));
    }
        

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
    public async Task<IActionResult> Update(string slug, [FromBody] UpdateServiceRequest request, CancellationToken ct)
    {
        return Ok(await serviceApp.UpdateAsync(slug, request, ct));
    }
        

    /// <summary>
    /// Enqueues status recomputation for all services. Runs asynchronously through the same
    /// channel check results use, so it serializes with other in-flight recomputations
    /// instead of racing them with a direct read-modify-write.
    /// </summary>
    [HttpPost("recompute-status")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RecomputeAll(CancellationToken ct)
    {
        var services = await serviceRepo.GetAllAsync(ct);
        foreach (var svc in services)
            statusChannel.Writer.TryWrite(new CheckStatusChangedEvent(0, svc.Id, ServiceStatus.NO_DATA, ServiceStatus.NO_DATA));
        return Accepted();
    }

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

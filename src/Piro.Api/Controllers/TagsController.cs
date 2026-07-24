using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>
/// Tag read/write and autocomplete for services, checks, and workers (RFC 0008, Part A). Reads are open;
/// writes require Owner/Admin. System (<c>piro:*</c>) tags are managed separately and cannot be set here.
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class TagsController(TagAppService tagApp) : ControllerBase
{
    /// <summary>Lists a service's own tags.</summary>
    [HttpGet("services/{id:int}/tags")]
    [ProducesResponseType<EntityTagsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceTags(int id, CancellationToken ct)
    {
        return Ok(await tagApp.GetServiceTagsAsync(id, ct));
    }

    /// <summary>Replaces a service's full user-tag set.</summary>
    [HttpPut("services/{id:int}/tags")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<EntityTagsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceServiceTags(int id, [FromBody] ReplaceTagsRequest request, CancellationToken ct)
    {
        return Ok(await tagApp.ReplaceServiceTagsAsync(id, request, ct));
    }

    /// <summary>Lists a check's own tags and its effective (inherited) tags.</summary>
    [HttpGet("checks/{id:int}/tags")]
    [ProducesResponseType<CheckTagsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCheckTags(int id, CancellationToken ct)
    {
        return Ok(await tagApp.GetCheckTagsAsync(id, ct));
    }

    /// <summary>Replaces a check's full user-tag set.</summary>
    [HttpPut("checks/{id:int}/tags")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<CheckTagsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceCheckTags(int id, [FromBody] ReplaceTagsRequest request, CancellationToken ct)
    {
        return Ok(await tagApp.ReplaceCheckTagsAsync(id, request, ct));
    }

    /// <summary>Lists a worker's own tags.</summary>
    [HttpGet("workers/{id:guid}/tags")]
    [ProducesResponseType<EntityTagsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkerTags(Guid id, CancellationToken ct)
    {
        return Ok(await tagApp.GetWorkerTagsAsync(id, ct));
    }

    /// <summary>Replaces a worker's full user-tag set.</summary>
    [HttpPut("workers/{id:guid}/tags")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<EntityTagsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceWorkerTags(Guid id, [FromBody] ReplaceTagsRequest request, CancellationToken ct)
    {
        return Ok(await tagApp.ReplaceWorkerTagsAsync(id, request, ct));
    }

    /// <summary>Autocomplete: distinct user tag keys, optionally filtered by prefix.</summary>
    [HttpGet("tags/keys")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKeys([FromQuery] string? prefix, CancellationToken ct)
    {
        return Ok(await tagApp.GetKeysAsync(prefix, ct));
    }

    /// <summary>Autocomplete: distinct values assigned for a given key.</summary>
    [HttpGet("tags/values")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetValues([FromQuery] string key, CancellationToken ct)
    {
        return Ok(await tagApp.GetValuesAsync(key, ct));
    }
}

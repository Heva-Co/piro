using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Enums;

namespace Piro.Api.Controllers;

/// <summary>Returns metadata about available check types from the CheckType manifest (RFC 0011).</summary>
[Authorize]
[ApiController]
[Route("api/v1/checks/types")]
[Produces("application/json")]
public class CheckTypesController(IEnumerable<ICheckExecutor> executors) : ControllerBase
{
    /// <summary>
    /// Returns all manifested check types with their display metadata, minimum interval, allowed
    /// alert-fors, required integration, and reflected config schema. Types with
    /// <c>requiredIntegrationType != null</c> need that Integration to exist; types with
    /// <c>hasExecutor == false</c> are declared but not yet runnable.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<CheckTypeMetaDto>>(StatusCodes.Status200OK)]
    public IActionResult GetTypes()
    {
        var runnable = executors.Select(e => e.CheckType).ToHashSet();

        var types = Enum.GetValues<CheckType>()
            .Select(t => t.ToMetaDto(hasExecutor: runnable.Contains(t)))
            .Where(dto => dto is not null);

        return Ok(types);
    }
}

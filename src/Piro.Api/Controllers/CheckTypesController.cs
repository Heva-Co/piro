using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Checks.Abstractions;

namespace Piro.Api.Controllers;

/// <summary>Returns metadata about available check types from the check registry (RFC 0011/0016).</summary>
[Authorize]
[ApiController]
[Route("api/v1/checks/types")]
[Produces("application/json")]
public class CheckTypesController(ICheckRegistry checkRegistry) : ControllerBase
{
    /// <summary>
    /// Returns every registered check type with its display metadata, minimum interval, alert
    /// dimensions, required integration, and reflected config schema. Types with
    /// <c>requiredIntegrationType != null</c> need that Integration to exist.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<CheckTypeMetaDto>>(StatusCodes.Status200OK)]
    public IActionResult GetTypes()
    {
        // Ordered A→Z by display name so the check-type picker is stable regardless of registration order.
        var types = checkRegistry.All
            .Select(c => c.ToMetaDto())
            .OrderBy(t => t.DisplayName ?? t.Type, StringComparer.OrdinalIgnoreCase);
        return Ok(types);
    }
}

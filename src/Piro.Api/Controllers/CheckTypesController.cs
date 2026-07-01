using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.Attributes;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Api.Controllers;

/// <summary>Returns metadata about available check types, including provider integration requirements.</summary>
[Authorize]
[ApiController]
[Route("api/v1/checks/types")]
[Produces("application/json")]
public class CheckTypesController(IEnumerable<ICheckExecutor> executors) : ControllerBase
{
    /// <summary>
    /// Returns all registered check types with their integration requirements.
    /// Types with <c>requiredIntegrationType != null</c> are only usable when an Integration of that type exists.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<CheckTypeMetaDto>>(StatusCodes.Status200OK)]
    public IActionResult GetTypes()
    {
        var types = executors.Select(e =>
        {
            var attr = e.GetType().GetCustomAttributes(typeof(RequiresIntegrationAttribute), inherit: false)
                           .Cast<RequiresIntegrationAttribute>()
                           .FirstOrDefault();
            return new CheckTypeMetaDto(
                e.CheckType.ToString(),
                attr?.IntegrationType.ToString()
            );
        });

        return Ok(types);
    }
}

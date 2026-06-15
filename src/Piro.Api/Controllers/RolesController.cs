using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Api.Controllers;

[ApiController]
[Route("api/v1/roles")]
[Produces("application/json")]
[Authorize]
public class RolesController(IUserManagementService userService) : ControllerBase
{
    /// <summary>Returns all available roles.</summary>
    [HttpGet]
    [ProducesResponseType<List<RoleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await userService.GetRolesAsync(ct));
}

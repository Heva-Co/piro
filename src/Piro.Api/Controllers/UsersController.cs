using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
[Authorize]
public class UsersController(IUserManagementService userService) : ControllerBase
{
    /// <summary>Lists all users with their roles.</summary>
    [HttpGet]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<List<UserListDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await userService.GetAllAsync(ct));

    /// <summary>Sends an email invitation to a new user.</summary>
    [HttpPost("invite")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest request, CancellationToken ct)
    {
        try
        {
            await userService.InviteAsync(request.Email, request.RoleId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    /// <summary>Accepts an invitation and activates the account. No authentication required.</summary>
    [HttpPost("accept-invite")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteRequest request, CancellationToken ct)
    {
        try
        {
            await userService.AcceptInviteAsync(request.Token, request.Name, request.Password, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    /// <summary>Changes a user's role. Owner-only.</summary>
    [HttpPut("{id:int}/role")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleRequest request, CancellationToken ct)
    {
        try
        {
            await userService.ChangeRoleAsync(id, request.RoleId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    /// <summary>Deletes a user. Owner and Admin can delete non-Owner users.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await userService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    /// <summary>Returns a single user by ID.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<UserListDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var users = await userService.GetAllAsync(ct);
        var user = users.FirstOrDefault(u => u.Id == id);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Returns a user's personal notification preferences.</summary>
    [HttpGet("{id:int}/notification-preferences")]
    [ProducesResponseType<List<UserNotificationPreferenceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotificationPreferences(int id, CancellationToken ct) =>
        Ok(await userService.GetNotificationPreferencesAsync(id, ct));

    /// <summary>Replaces all personal notification preferences for a user.</summary>
    [HttpPut("{id:int}/notification-preferences")]
    [ProducesResponseType<List<UserNotificationPreferenceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetNotificationPreferences(
        int id, [FromBody] SetUserNotificationPreferencesRequest request, CancellationToken ct) =>
        Ok(await userService.SetNotificationPreferencesAsync(id, request, ct));
}

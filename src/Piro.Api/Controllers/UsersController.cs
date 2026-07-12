using System.Security.Claims;
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
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            await userService.DeleteAsync(id, currentUserId, ct);
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
        var user = await userService.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Returns a user's personal notification preferences.</summary>
    [HttpGet("{id:int}/notification-preferences")]
    [ProducesResponseType<List<UserNotificationPreferenceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotificationPreferences(int id, CancellationToken ct) =>
        Ok(await userService.GetNotificationPreferencesAsync(id, ct));

    /// <summary>Creates a new personal notification preference, appended at the lowest priority.</summary>
    [HttpPost("{id:int}/notification-preferences")]
    [ProducesResponseType<UserNotificationPreferenceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateNotificationPreference(
        int id, [FromBody] UpsertUserNotificationPreferenceRequest request, CancellationToken ct)
    {
        var created = await userService.CreateNotificationPreferenceAsync(id, request, ct);
        return CreatedAtAction(nameof(GetNotificationPreferences), new { id }, created);
    }

    /// <summary>Edits an existing personal notification preference's channel/integration/handle.</summary>
    [HttpPut("{id:int}/notification-preferences/{preferenceId:int}")]
    [ProducesResponseType<UserNotificationPreferenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateNotificationPreference(
        int id, int preferenceId, [FromBody] UpsertUserNotificationPreferenceRequest request, CancellationToken ct) =>
        Ok(await userService.UpdateNotificationPreferenceAsync(id, preferenceId, request, ct));

    /// <summary>Reorders a user's personal notification preferences.</summary>
    [HttpPut("{id:int}/notification-preferences/reorder")]
    [ProducesResponseType<List<UserNotificationPreferenceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReorderNotificationPreferences(
        int id, [FromBody] ReorderUserNotificationPreferencesRequest request, CancellationToken ct) =>
        Ok(await userService.ReorderNotificationPreferencesAsync(id, request, ct));

    /// <summary>Deletes a personal notification preference. Rejects the account-fallback preference.</summary>
    [HttpDelete("{id:int}/notification-preferences/{preferenceId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNotificationPreference(int id, int preferenceId, CancellationToken ct)
    {
        await userService.DeleteNotificationPreferenceAsync(id, preferenceId, ct);
        return NoContent();
    }

    /// <summary>Sends a one-time verification code to an already-saved (pending) preference's handle.</summary>
    [HttpPost("{id:int}/notification-preferences/{preferenceId:int}/verify/send")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendNotificationPreferenceCode(int id, int preferenceId, CancellationToken ct)
    {
        await userService.SendNotificationPreferenceCodeAsync(id, preferenceId, ct);
        return NoContent();
    }

    /// <summary>Confirms the verification code for a pending preference.</summary>
    [HttpPost("{id:int}/notification-preferences/{preferenceId:int}/verify/confirm")]
    [ProducesResponseType<UserNotificationPreferenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmNotificationPreferenceCode(
        int id, int preferenceId, [FromBody] ConfirmNotificationPreferenceCodeRequest request, CancellationToken ct) =>
        Ok(await userService.ConfirmNotificationPreferenceCodeAsync(id, preferenceId, request.Code, ct));
}

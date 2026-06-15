using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Api.Controllers;

/// <summary>First-run setup wizard. Accessible only when no Owner account exists yet.</summary>
[ApiController]
[Route("api/v1/setup")]
[Produces("application/json")]
public class SetupController(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager) : ControllerBase
{
    private const string OwnerRole = "Owner";

    /// <summary>Returns whether initial setup is still required.</summary>
    [HttpGet("status")]
    [ProducesResponseType<SetupStatusResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var required = !await HasOwnerAsync();
        return Ok(new SetupStatusResponse(required));
    }

    /// <summary>
    /// Creates the Owner account and built-in roles. Can only be called once —
    /// subsequent calls are rejected if an Owner already exists.
    /// </summary>
    [HttpPost("complete")]
    [ProducesResponseType<SignInResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete([FromBody] CompleteSetupRequest request, CancellationToken ct)
    {
        if (await HasOwnerAsync())
            return Conflict(new { title = "Setup already completed.", status = 409 });

        // Seed built-in roles
        await SeedRolesAsync();

        // Create owner user
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            Name = request.Name,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(new { title = "Failed to create owner account.", detail = errors, status = 400 });
        }

        await userManager.AddToRoleAsync(user, OwnerRole);

        return Ok(new SetupStatusResponse(false));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<bool> HasOwnerAsync()
    {
        var owners = await userManager.GetUsersInRoleAsync(OwnerRole);
        return owners.Count > 0;
    }

    private async Task SeedRolesAsync()
    {
        string[] roles = [OwnerRole, "Admin", "Member", "Viewer"];
        foreach (var name in roles)
        {
            if (!await roleManager.RoleExistsAsync(name))
                await roleManager.CreateAsync(new AppRole { Name = name, IsReadonly = true });
        }
    }
}

public record SetupStatusResponse(bool SetupRequired);
public record CompleteSetupRequest(string Email, string Password, string Name);

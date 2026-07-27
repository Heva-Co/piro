using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Api.Controllers;

/// <summary>
/// Registration of the current user's mobile devices for on-call push (the Piro mobile app). The app
/// obtains its FCM/APNs token, then registers it here; escalation then pages every registered device.
/// </summary>
[ApiController]
[Route("api/v1/devices")]
[Produces("application/json")]
[Authorize]
public class DevicesController(IDeviceRegistrationService deviceService) : ControllerBase
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Registers (or refreshes) the calling user's device push token. Idempotent.</summary>
    [HttpPost]
    [ProducesResponseType<DeviceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { title = "A device token is required.", status = 400 });

        var device = await deviceService.RegisterAsync(
            CurrentUserId, request.Platform, request.Token, request.DeviceName, request.PushPublicKey, ct);
        return Ok(device);
    }

    /// <summary>Lists the calling user's registered devices.</summary>
    [HttpGet]
    [ProducesResponseType<List<DeviceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken ct) =>
        Ok(await deviceService.GetDevicesAsync(CurrentUserId, ct));

    /// <summary>
    /// Unregisters a device push token (device sign-out). The token is passed as a query parameter
    /// because push tokens may contain characters ('/') that don't round-trip safely in a path segment.
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Unregister([FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { title = "A device token is required.", status = 400 });

        await deviceService.UnregisterAsync(CurrentUserId, token, ct);
        return NoContent();
    }
}

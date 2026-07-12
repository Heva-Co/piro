using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

[ApiController]
[Route("api/v1/oncall/schedules")]
[Produces("application/json")]
[Authorize]
public class OnCallSchedulesController(OnCallScheduleAppService scheduleService) : ControllerBase
{
    /// <summary>The current user's own on-call slots across every schedule they appear in — for their personal calendar.</summary>
    [HttpGet("me/slots")]
    [ProducesResponseType<List<OnCallSlotDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySlots(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct = default)
    {
        if (to <= from) return BadRequest("'to' must be after 'from'.");
        if (to - from > TimeSpan.FromDays(366)) return BadRequest("Range cannot exceed 366 days.");
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var slots = await scheduleService.GetMySlotsAsync(userId, from, to, ct);
        return Ok(slots);
    }

    /// <summary>Whether the current user is on-call right now, and for which schedule.</summary>
    [HttpGet("me/current")]
    [ProducesResponseType<OnCallSlotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetMyCurrentStatus(CancellationToken ct = default)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var status = await scheduleService.GetMyCurrentStatusAsync(userId, ct);
        return status is null ? NoContent() : Ok(status);
    }

    [HttpGet]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<OnCallSchedulePageDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await scheduleService.GetPagedAsync(page, pageSize, ct));

    /// <summary>Lightweight schedule list — name and unique roster only, for pickers that don't need rotation detail.</summary>
    [HttpGet("members")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<List<OnCallScheduleMembersDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllWithMembers(CancellationToken ct) =>
        Ok(await scheduleService.GetAllWithMembersAsync(ct));

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<OnCallScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await scheduleService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<OnCallScheduleDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateOnCallScheduleRequest request, CancellationToken ct)
    {
        var dto = await scheduleService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<OnCallScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOnCallScheduleRequest request, CancellationToken ct) =>
        Ok(await scheduleService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await scheduleService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Returns who is currently on-call for this schedule.</summary>
    [HttpGet("{id:int}/current")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<List<OnCallUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(int id, CancellationToken ct)
    {
        var users = await scheduleService.GetCurrentOnCallUsersAsync(id, ct);
        return Ok(users.Select(u => new OnCallUserDto(u.Id, u.Name, GetInitials(u.Name), u.Color)));
    }

    /// <summary>Expands the schedule into Gantt slots for the given time range.</summary>
    [HttpGet("{id:int}/expand")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<List<OnCallSlotDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Expand(
        int id,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] bool applyOverrides = true,
        CancellationToken ct = default)
    {
        if (to <= from) return BadRequest("'to' must be after 'from'.");
        if (to - from > TimeSpan.FromDays(366)) return BadRequest("Range cannot exceed 366 days.");
        var slots = await scheduleService.ExpandAsync(id, from, to, applyOverrides, ct);
        return Ok(slots);
    }

    /// <summary>
    /// Resolves a draft batch of rotation/override changes into slots and coverage gaps over the
    /// given range, without persisting anything — lets the "Save" button warn about uncovered
    /// windows before the user confirms.
    /// </summary>
    [HttpPost("{id:int}/rotations/preview")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<RotationsPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewRotations(
        int id, [FromBody] SaveRotationsRequest request,
        [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken ct)
    {
        if (to <= from) return BadRequest("'to' must be after 'from'.");
        if (to - from > TimeSpan.FromDays(366)) return BadRequest("Range cannot exceed 366 days.");
        var dto = await scheduleService.PreviewRotationsAsync(id, request, from, to, ct);
        return Ok(dto);
    }

    /// <summary>
    /// Applies a batch of rotation layer and override changes atomically — either every
    /// operation in the request succeeds, or none of them do. Backs the "Save" button on the
    /// schedule detail page, which lets the user stage several edits before persisting them.
    /// </summary>
    [HttpPut("{id:int}/rotations")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<OnCallScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveRotations(int id, [FromBody] SaveRotationsRequest request, CancellationToken ct)
    {
        var dto = await scheduleService.SaveRotationsAsync(id, request, ct);
        return Ok(dto);
    }

    [HttpPost("{id:int}/layers")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<OnCallLayerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateLayer(int id, [FromBody] CreateOnCallLayerRequest request, CancellationToken ct)
    {
        var dto = await scheduleService.CreateLayerAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    [HttpPut("{id:int}/layers/{layerId:int}")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<OnCallLayerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLayer(int id, int layerId, [FromBody] UpdateOnCallLayerRequest request, CancellationToken ct)
    {
        var dto = await scheduleService.UpdateLayerAsync(id, layerId, request, ct);
        return Ok(dto);
    }

    [HttpDelete("{id:int}/layers/{layerId:int}")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLayer(int id, int layerId, CancellationToken ct)
    {
        await scheduleService.DeleteLayerAsync(id, layerId, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/overrides")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<OnCallOverrideDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateOverride(int id, [FromBody] CreateOnCallOverrideRequest request, CancellationToken ct)
    {
        var dto = await scheduleService.CreateOverrideAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    [HttpDelete("{id:int}/overrides/{overrideId:int}")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOverride(int id, int overrideId, CancellationToken ct)
    {
        await scheduleService.DeleteOverrideAsync(id, overrideId, ct);
        return NoContent();
    }

    private static string GetInitials(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
        };
    }
}

public record OnCallUserDto(int Id, string Name, string Initials, string Color);

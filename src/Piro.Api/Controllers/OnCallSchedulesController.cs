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
    [HttpGet]
    [ProducesResponseType<List<OnCallScheduleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await scheduleService.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<OnCallScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await scheduleService.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<OnCallScheduleDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateOnCallScheduleRequest request, CancellationToken ct)
    {
        var dto = await scheduleService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<OnCallScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOnCallScheduleRequest request, CancellationToken ct) =>
        Ok(await scheduleService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await scheduleService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Returns who is currently on-call for this schedule.</summary>
    [HttpGet("{id:int}/current")]
    [ProducesResponseType<List<OnCallUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(int id, CancellationToken ct)
    {
        var users = await scheduleService.GetCurrentOnCallUsersAsync(id, ct);
        return Ok(users.Select(u => new OnCallUserDto(u.Id, u.Name, GetInitials(u.Name), u.Color)));
    }

    /// <summary>Expands the schedule into Gantt slots for the given time range.</summary>
    [HttpGet("{id:int}/expand")]
    [ProducesResponseType<List<OnCallSlotDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Expand(
        int id,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] bool applyOverrides = true,
        CancellationToken ct = default)
    {
        if (to <= from) return BadRequest("'to' must be after 'from'.");
        var slots = await scheduleService.ExpandAsync(id, from, to, applyOverrides, ct);
        return Ok(slots);
    }

    [HttpPost("{id:int}/layers")]
    [ProducesResponseType<OnCallLayerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateLayer(int id, [FromBody] CreateOnCallLayerRequest request, CancellationToken ct)
    {
        var dto = await scheduleService.CreateLayerAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    [HttpPut("{id:int}/layers/{layerId:int}")]
    [ProducesResponseType<OnCallLayerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLayer(int id, int layerId, [FromBody] UpdateOnCallLayerRequest request, CancellationToken ct)
    {
        var dto = await scheduleService.UpdateLayerAsync(id, layerId, request, ct);
        return Ok(dto);
    }

    [HttpDelete("{id:int}/layers/{layerId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLayer(int id, int layerId, CancellationToken ct)
    {
        await scheduleService.DeleteLayerAsync(id, layerId, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/overrides")]
    [ProducesResponseType<OnCallOverrideDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateOverride(int id, [FromBody] CreateOnCallOverrideRequest request, CancellationToken ct)
    {
        var dto = await scheduleService.CreateOverrideAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    [HttpDelete("{id:int}/overrides/{overrideId:int}")]
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

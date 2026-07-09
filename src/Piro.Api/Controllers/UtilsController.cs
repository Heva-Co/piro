using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Piro.Api.Controllers;

/// <summary>General-purpose utility endpoints (timezones, etc.).</summary>
[ApiController]
[Route("api/v1/utils")]
[Produces("application/json")]
[AllowAnonymous]
public class UtilsController : ControllerBase
{
    /// <summary>Returns all IANA timezones available on the server, sorted by UTC offset then name.</summary>
    [HttpGet("timezones")]
    public IActionResult GetTimezones()
    {
        var now = DateTimeOffset.UtcNow;
        var zones = TimeZoneInfo.GetSystemTimeZones()
            .Select(tz =>
            {
                var offset = tz.GetUtcOffset(now);
                var sign = offset < TimeSpan.Zero ? "-" : "+";
                var formatted = $"GMT{sign}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";
                return new TimezoneDto(tz.Id, tz.DisplayName, formatted, (int)offset.TotalMinutes);
            })
            .OrderBy(t => t.OffsetMinutes)
            .ThenBy(t => t.Id)
            .ToList();
        return Ok(zones);
    }
}

public record TimezoneDto(string Id, string DisplayName, string Offset, int OffsetMinutes);

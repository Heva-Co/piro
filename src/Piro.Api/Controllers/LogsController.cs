using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Api.Controllers;

/// <summary>Exposes Piro application logs.</summary>
[ApiController]
[Route("api/v1/logs")]
[Authorize]
public class LogsController(ILogRepository logRepository) : ControllerBase
{
    /// <summary>Returns a paginated list of application log entries.</summary>
    [HttpGet]
    public async Task<ActionResult<LogPageDto>> GetLogs(
        [FromQuery] string? level = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? checkId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await logRepository.GetPagedAsync(
            new LogQueryParams(level, search, from, to, checkId, page, pageSize), ct);
        return Ok(result);
    }
}

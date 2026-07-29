using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Enums;

namespace Piro.Api.Controllers;

/// <summary>
/// Exposes the audit trail (issue #17). Read-only: the table is append-only, so there is
/// deliberately no endpoint that writes, edits or deletes an entry.
/// </summary>
/// <remarks>
/// Restricted to Owner and Admin. The trail records who changed what across the whole instance,
/// including identity and access configuration, so it is administrative rather than operational.
/// </remarks>
[ApiController]
[Route("api/v1/audit-logs")]
[Authorize(Roles = "Owner,Admin")]
public class AuditLogsController(IAuditLogRepository auditLogRepository) : ControllerBase
{
    /// <summary>
    /// Returns a paginated list of audit transactions, newest first. A page holds whole
    /// transactions, so <c>totalCount</c> counts user actions rather than individual entries.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AuditLogPageDto>> GetAuditLogs(
        [FromQuery] string? entityType = null,
        [FromQuery] string? userId = null,
        [FromQuery] AuditAction? action = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await auditLogRepository.GetPagedAsync(
            new AuditLogQueryParams(entityType, userId, action, from, to, page, pageSize), ct);

        return Ok(result);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;
using Piro.Domain.Enums;

namespace Piro.Api.Controllers;

/// <summary>
/// Read-only feed of notification delivery attempts (RFC 0009 §6) — the admin's answer to
/// "why didn't my notification arrive?". Its own resource, distinct from subscription CRUD and from
/// the system (PiroLog) logs, though grouped under /logs in the URL and under "Logs" in the admin nav.
/// </summary>
[ApiController]
[Route("api/v1/logs/deliveries")]
[Produces("application/json")]
[Authorize(Roles = "Owner,Admin")]
public class DeliveryLogsController(DeliveryLogAppService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationDeliveryLogPageDto>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] DeliveryStatus? status = null, CancellationToken ct = default) =>
        Ok(await service.GetPagedAsync(page, pageSize, status, ct));
}

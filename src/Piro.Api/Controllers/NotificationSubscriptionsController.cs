using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

[ApiController]
[Route("api/v1/event-subscriptions")]
[Produces("application/json")]
[Authorize(Roles = "Owner,Admin")]
public class NotificationSubscriptionsController(NotificationSubscriptionAppService service) : ControllerBase
{
    /// <summary>The closed catalog of events a subscription can fire on (RFC 0009 §4.2).</summary>
    [HttpGet("events")]
    public ActionResult<IReadOnlyList<NotificationEventCatalogDto>> GetEventCatalog() =>
        Ok(service.GetEventCatalog());

    [HttpGet]
    public async Task<ActionResult<NotificationSubscriptionPageDto>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await service.GetPagedAsync(page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationSubscriptionDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<NotificationSubscriptionDto>> Create(
        [FromBody] UpsertNotificationSubscriptionRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NotificationSubscriptionDto>> Update(
        Guid id, [FromBody] UpsertNotificationSubscriptionRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

[ApiController]
[Route("api/v1/escalation-policies")]
[Produces("application/json")]
[Authorize(Roles = "Owner,Admin")]
public class EscalationPoliciesController(EscalationPolicyAppService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EscalationPolicyPageDto>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await service.GetPagedAsync(page, pageSize, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EscalationPolicyDto>> GetById(int id, CancellationToken ct) =>
        Ok(await service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<EscalationPolicyDto>> Create(
        [FromBody] UpsertEscalationPolicyRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EscalationPolicyDto>> Update(
        int id, [FromBody] UpsertEscalationPolicyRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

[ApiController]
[Route("api/v1/escalation-policy")]
[Produces("application/json")]
[Authorize(Roles = "Owner,Admin")]
public class EscalationPoliciesController(EscalationPolicyAppService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EscalationPolicyDto>> Get(CancellationToken ct)
    {
        var policy = await service.GetAsync(ct);
        if (policy is null) return NotFound();
        return Ok(policy);
    }

    [HttpPut]
    public async Task<ActionResult<EscalationPolicyDto>> Upsert(
        [FromBody] UpsertEscalationPolicyRequest request, CancellationToken ct)
    {
        var result = await service.UpsertAsync(request, ct);
        return Ok(result);
    }
}

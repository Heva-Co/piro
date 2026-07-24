using Microsoft.AspNetCore.Mvc;
using Piro.Api.Extensions;
using Piro.Application.Interfaces;
using Piro.Checks.Abstractions;

namespace Piro.Api.Controllers;

/// <summary>
/// The single generic inbound endpoint for push-based checks (RFC 0013). Unauthenticated at the pipeline
/// level (no cookie/API-key auth) exactly like <c>WebhooksController</c>: the check's handler self-validates
/// its own token. Resolves the check by the URL id, then dispatches to the <see cref="ICheckInboundHandler"/>
/// its type ships — so the endpoint functionally exists only because the check is registered. An unknown
/// check, or a check whose type ships no inbound handler (e.g. the type isn't installed), is a 404.
/// </summary>
[ApiController]
[Route("api/v1/checks/{checkId:int}/inbound")]
public class CheckInboundController(ICheckRepository checkRepository, ICheckInboundDispatcher dispatcher) : ControllerBase
{
    [HttpGet("{**rest}")]
    [HttpPost("{**rest}")]
    public async Task<IActionResult> Receive(int checkId, string? rest, CancellationToken ct)
    {
        var check = await checkRepository.GetByIdAsync(checkId, ct);
        if (check is null)
            return NotFound();

        var rawBody = HttpMethods.IsPost(Request.Method) ? await Request.ReadBodyAsStringAsync(ct) : "";
        var ctx = new CheckInboundContext(
            Rest: rest ?? "",
            RawBody: rawBody,
            Query: Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            Headers: Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase));

        var outcome = await dispatcher.DispatchAsync(check.Id, check.Type.ToString(), ctx, ct);
        // No handler for this check's type → the inbound endpoint doesn't exist for it.
        if (outcome is null)
            return NotFound();

        return outcome switch
        {
            CheckInboundOutcome.Accepted => NoContent(),
            CheckInboundOutcome.AuthFailed => Unauthorized(),
            _ => BadRequest(),
        };
    }
}

using Microsoft.AspNetCore.Mvc;
using Piro.Api.Extensions;
using Piro.Application.Services;
using Piro.Domain.Enums;

namespace Piro.Api.Controllers;

/// <summary>
/// Inbound third-party webhook endpoints — RFC 0001. Unauthenticated at the ASP.NET Core pipeline
/// level (no cookie/API-key auth applies here); each endpoint validates its own per-Integration
/// token instead, since the source platform controls how it authenticates outbound webhook calls.
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
public class WebhooksController(GcpWebhookIngestionService gcpIngestionService) : ControllerBase
{
    /// <summary>
    /// Receives a GCP Cloud Monitoring alerting policy notification — RFC 0001 §4.8. Auth is a
    /// query-string token (<c>auth_token</c>), since GCP's webhook notification channel supports
    /// no custom headers. Always returns 200 for a request that was at least parseable, even if it
    /// didn't produce an Alert, to avoid GCP retry-storming an endpoint that will never accept it;
    /// only a wrong/missing token or an unknown Integration get a non-2xx.
    /// </summary>
    [HttpPost("gcp/{integrationId:guid}")]
    public async Task<IActionResult> ReceiveGcpCloudMonitoring(
        Guid integrationId,
        [FromQuery(Name = "auth_token")] string? authToken,
        CancellationToken ct)
    {
        var rawPayload = await Request.ReadBodyAsStringAsync(ct);

        var outcome = await gcpIngestionService.IngestAsync(integrationId, authToken, rawPayload, ct);

        return outcome switch
        {
            WebhookRequestOutcome.AuthFailed => Unauthorized(),
            _ => Ok(),
        };
    }
}

using Microsoft.AspNetCore.Mvc;
using Piro.Api.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Integrations.Abstractions;

namespace Piro.Api.Controllers;

/// <summary>
/// Inbound third-party webhook endpoint — RFC 0001/0016. Unauthenticated at the ASP.NET Core pipeline
/// level (no cookie/API-key auth applies); each integration's handler validates its own per-instance
/// token. A single generic route <c>{integrationId}/{**rest}</c> resolves the integration instance from
/// the URL, looks up its registered <see cref="IInboundWebhookHandler"/>, and dispatches — no per-source
/// endpoint. An unknown instance or a type with no webhook handler is a 404 (the link is wrong).
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
public class WebhooksController(
    IIntegrationRepository integrationRepository,
    IWebhookRequestLogRepository webhookLogRepository,
    IInboundWebhookRegistry webhookRegistry,
    IIntegrationHost integrationHost) : ControllerBase
{
    [HttpPost("{integrationId:guid}/{**rest}")]
    public async Task<IActionResult> Receive(Guid integrationId, string? rest, CancellationToken ct)
    {
        var integration = await integrationRepository.GetByIdAsync(integrationId, ct);
        if (integration is null)
            return NotFound();

        var handler = webhookRegistry.Resolve(integration.Type);
        if (handler is null)
            return NotFound();

        if (!TryMatchTemplate(handler.WebhookPathTemplate, rest, out var routeValues))
            return NotFound();

        var rawPayload = await Request.ReadBodyAsStringAsync(ct);
        var ctx = new InboundWebhookContext(
            integrationId,
            rawPayload,
            Query: Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            Headers: Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            RouteValues: routeValues);

        var log = new WebhookRequestLog
        {
            IntegrationId = integrationId,
            ReceivedAt = DateTimeOffset.UtcNow,
            RawPayload = rawPayload,
        };

        var outcome = await handler.HandleAsync(ctx, integrationHost, ct);

        log.Outcome = MapOutcome(outcome);
        await webhookLogRepository.CreateAsync(log, ct);

        // GCP-style sources retry on non-2xx, so only a genuine reject (bad auth) is non-2xx; a parseable
        // request that produced nothing still gets 200 to avoid a retry storm.
        return outcome == WebhookOutcome.AuthFailed ? Unauthorized() : Ok();
    }

    /// <summary>
    /// Matches the URL segment(s) after the instance id against the handler's template. Empty template
    /// matches only an empty/absent rest; "{region}/{env}" captures each segment by name; a literal
    /// segment must match verbatim. Segment count must line up exactly.
    /// </summary>
    private static bool TryMatchTemplate(string template, string? rest, out IReadOnlyDictionary<string, string> values)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        values = result;

        var templateSegments = string.IsNullOrEmpty(template)
            ? []
            : template.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var restSegments = string.IsNullOrEmpty(rest)
            ? []
            : rest.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (templateSegments.Length != restSegments.Length)
            return false;

        for (var i = 0; i < templateSegments.Length; i++)
        {
            var t = templateSegments[i];
            if (t.StartsWith('{') && t.EndsWith('}'))
                result[t[1..^1]] = restSegments[i];
            else if (!string.Equals(t, restSegments[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static WebhookRequestOutcome MapOutcome(WebhookOutcome outcome) => outcome switch
    {
        WebhookOutcome.AuthFailed => WebhookRequestOutcome.AuthFailed,
        WebhookOutcome.ParseError => WebhookRequestOutcome.ParseError,
        _ => WebhookRequestOutcome.AcceptedOrphan,
    };
}

using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// One row per inbound third-party webhook request, written before auth/parsing so rejected or
/// malformed requests are captured too — not just successful ones. Lets an admin answer "how many
/// POSTs did this webhook get, what did they look like, how many became alerts" directly, instead
/// of grepping application logs. See RFC 0001 §4.4.
/// </summary>
public class WebhookRequestLog
{
    public int Id { get; set; }
    public Guid IntegrationId { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>Exact request body, unmodified — the source of truth <see cref="Alert.SourceRequestLogId"/> points at.</summary>
    public string RawPayload { get; set; } = string.Empty;

    public WebhookRequestOutcome Outcome { get; set; }

    /// <summary>Set only when this request produced or updated an Alert (Accepted/AcceptedOrphan/CorrelationMismatch/Deduplicated).</summary>
    public int? AlertId { get; set; }

    public Integration Integration { get; set; } = null!;
    public Alert? Alert { get; set; }
}

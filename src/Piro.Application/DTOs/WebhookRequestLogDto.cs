using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>Wire representation of a single inbound webhook request — see RFC 0001 §4.4.</summary>
public record WebhookRequestLogDto(
    int Id,
    DateTimeOffset ReceivedAt,
    string RawPayload,
    WebhookRequestOutcome Outcome,
    int? AlertId
);

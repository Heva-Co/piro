using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>
/// The wire shape of an outbound external reference for a local object (RFC 0012 §4.5) — what the
/// detail page renders as "🔗 OPS-123 ↗". <see cref="Metadata"/> is the provider-specific blob as
/// stored, opaque to Piro and to this DTO; the frontend does not interpret it.
/// </summary>
public sealed record ExternalReferenceDto(
    ActionContext Context,
    int TargetId,
    Guid IntegrationId,
    string ActionId,
    string ExternalId,
    string Url,
    string Label,
    IReadOnlyDictionary<string, object?>? Metadata);

using System.Text.Json;
using Piro.Domain.Enums;
using Piro.Contracts;

namespace Piro.Application.DTOs;

/// <summary>
/// The execute request body for a user-initiated integration action (RFC 0012 §4.4): the target the
/// action was invoked from and the human-supplied input. <see cref="Input"/> is a raw JSON element
/// deserialized server-side into the action's InputType and validated with its DataAnnotations — the
/// same annotations that drove the dialog, so form and payload can't drift.
/// </summary>
public sealed record ExecuteIntegrationActionRequest(
    UISurface Context,
    int TargetId,
    JsonElement? Input);

/// <summary>The external reference an executed action produced (RFC 0012 §4.5), returned to the client.</summary>
public sealed record IntegrationActionResultDto(
    string ExternalId,
    string Url,
    string Label);

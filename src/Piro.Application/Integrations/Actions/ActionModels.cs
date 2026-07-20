using Piro.Domain.Enums;

namespace Piro.Application.Integrations.Actions;

/// <summary>
/// A neutral, read-only view of the local object an action was invoked from (RFC 0012 §4.6).
/// Deliberately <b>not</b> the EF entity: an action reads its target only through this projection, so
/// it can never mutate an Alert/Incident/Maintenance and is not coupled to their concrete shapes.
/// <see cref="IActionHost.GetTargetAsync"/> builds it by switching on <see cref="Context"/>.
/// </summary>
public sealed record ActionTarget(
    ActionContext Context,
    int Id,
    string Title,
    string Summary,
    string PiroUrl);

/// <summary>
/// Everything a handler needs to run: the resolved integration id, the target, and the deserialized
/// human input (null for a draft build or a no-input action). The handler reaches the rest of Piro
/// only through <see cref="IActionHost"/> — never a repository or <c>DbContext</c> directly.
/// </summary>
public sealed record ActionExecutionContext(
    Guid IntegrationId,
    ActionContext Context,
    int TargetId,
    object? Input);

/// <summary>The outbound reference an action produced, persisted as an <c>ExternalReference</c> (RFC 0012 §4.5).</summary>
public sealed record ActionResult(string ExternalId, string Url, string Label);

/// <summary>A request to attach an outbound external reference to a local object — the write side of <see cref="IActionHost.LinkExternalAsync"/>.</summary>
public sealed record ExternalReferenceRequest(
    ActionContext Context,
    int TargetId,
    Guid IntegrationId,
    string ActionId,
    string ExternalId,
    string Url,
    string Label);

/// <summary>A read-only view of a previously-created external reference (for gating and dedup, RFC 0012 §4.5).</summary>
public sealed record ExternalReferenceView(
    ActionContext Context,
    int TargetId,
    Guid IntegrationId,
    string ActionId,
    string ExternalId,
    string Url,
    string Label);

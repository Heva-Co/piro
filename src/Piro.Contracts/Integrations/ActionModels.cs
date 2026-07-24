
namespace Piro.Contracts;

/// <summary>
/// A neutral, read-only view of the local object an action was invoked from (RFC 0012 §4.6).
/// Deliberately <b>not</b> the EF entity: an action reads its target only through this projection, so
/// it can never mutate an Alert/Incident/Maintenance and is not coupled to their concrete shapes. The
/// executor resolves it and hands it to the action inside <see cref="UIActionContext"/> ("Forma 1",
/// RFC 0016) — the action never asks Piro to load it.
/// </summary>
public sealed record ActionTarget(
    UISurface Context,
    int Id,
    string Title,
    string Summary,
    string PiroUrl);

/// <summary>
/// Everything a handler needs to run, resolved and handed to it by the executor ("Forma 1", RFC 0016):
/// the integration instance id, the already-resolved <see cref="Target"/> (title/summary/URL of the
/// Alert/Incident/Maintenance the button was on), and the deserialized human input (null for a draft
/// build or a no-input action). The handler reads everything from here — it never loads the target
/// itself, touches a repository, or sees the <c>DbContext</c>.
/// </summary>
public sealed record UIActionContext(
    Guid IntegrationId,
    ActionTarget Target,
    object? Input)
{
    /// <summary>Convenience accessor: the surface (Alert/Incident/Maintenance) the action was invoked from.</summary>
    public UISurface Context => Target.Context;

    /// <summary>Convenience accessor: the local target's id.</summary>
    public int TargetId => Target.Id;
}

/// <summary>
/// The outbound reference an action produced, persisted as an <c>ExternalReference</c> (RFC 0012 §4.5).
/// The three fixed fields are universal (any external thing has an id, a URL, a display label); provider-
/// specific coordinates an integration needs to keep (e.g. Slack's message ts, Linear's team id) go in
/// <see cref="Metadata"/> — an opaque blob Piro stores and hands back untouched, never interprets. This
/// is the escape valve that keeps the table and the host contract from ever growing a per-provider field.
/// </summary>
public sealed record UIActionResult(
    string ExternalId,
    string Url,
    string Label,
    IReadOnlyDictionary<string, object?>? Metadata = null);

/// <summary>A request to attach an outbound external reference to a local object — the write side of the external-reference store the executor persists through.</summary>
public sealed record ExternalReferenceRequest(
    UISurface Context,
    int TargetId,
    Guid IntegrationId,
    string ActionId,
    string ExternalId,
    string Url,
    string Label,
    IReadOnlyDictionary<string, object?>? Metadata = null);

/// <summary>
/// A read-only view of a previously-created external reference (for gating and dedup, RFC 0012 §4.5).
/// <see cref="Metadata"/> is the provider-specific blob as stored — opaque to Piro, meaningful only to
/// the integration that wrote it.
/// </summary>
public sealed record ExternalReferenceView(
    UISurface Context,
    int TargetId,
    Guid IntegrationId,
    string ActionId,
    string ExternalId,
    string Url,
    string Label,
    IReadOnlyDictionary<string, object?>? Metadata = null);

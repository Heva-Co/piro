using Piro.Domain.Enums;
using Piro.Contracts;

namespace Piro.Domain.Entities;

/// <summary>
/// An outbound link Piro recorded for a local object: "an action of this integration created external
/// thing X (e.g. Jira OPS-123) for this Alert/Incident/Maintenance, here is its URL" (RFC 0012 §4.5).
/// The counterpart to <c>Alert.ExternalId</c>/<c>SourceUrl</c>, which are <i>inbound</i> (RFC 0001) —
/// this table is deliberately separate so inbound dedup semantics aren't overloaded.
/// <para>
/// One polymorphic table rather than columns-per-entity or per-integration: the alternative forces a
/// migration for every (entity × integration) pair and can't represent "an object with both a Jira and a
/// future Linear ticket."
/// </para>
/// </summary>
public class ExternalReference
{
    public int Id { get; set; }

    /// <summary>Which kind of local object this points at (Alert/Incident/Maintenance). Polymorphic — deliberately not a FK; there is no common base to reference.</summary>
    public UISurface TargetType { get; set; }

    /// <summary>The local object's int Id (Alert.Id / Incident.Id / Maintenance.Id — all int).</summary>
    public int TargetId { get; set; }

    /// <summary>The integration that created this reference. Real FK (Guid), OnDelete Cascade — deleting the integration drops its references.</summary>
    public Guid IntegrationId { get; set; }
    public Integration Integration { get; set; } = null!;

    /// <summary>Which action created it ("create-issue") — part of gating and dedup keys.</summary>
    public string ActionId { get; set; } = string.Empty;

    /// <summary>The external system's opaque id ("OPS-123", an issue number, …). Piro stores it as a string, never parses it.</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>Deep link into the external system.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Display text for the link ("OPS-123").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Provider-specific coordinates as an opaque JSON blob — the escape valve that lets an integration
    /// keep whatever it needs (Slack's message ts, Linear's team id, …) without the table or the host
    /// contract growing a per-provider field. Piro stores and returns it verbatim; only the integration
    /// that wrote it interprets it. Defaults to <c>"{}"</c>.
    /// </summary>
    public string MetadataJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

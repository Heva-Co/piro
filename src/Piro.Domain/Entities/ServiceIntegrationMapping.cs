namespace Piro.Domain.Entities;

/// <summary>
/// Links a Piro <see cref="Service"/> to an <see cref="Integration"/> and records the remote entity
/// that integration should act on for this service (RFC 0004 §4.5). Both the N:M link and the mapping
/// are the same row — the link exists precisely to say "this Piro service corresponds to that remote thing."
/// <para>
/// <see cref="MappingJson"/> holds provider-specific <i>remote</i> coordinates (identifiers that live in
/// the third party, not in Piro — so there's nothing to foreign-key to): for PagerDuty,
/// <c>{ "pagerDutyServiceId": "...", "routingKey": "..." }</c>. Each provider deserializes its own typed
/// mapping class from it, mirroring the <c>Integration.ConfigJson</c> pattern one level down.
/// </para>
/// </summary>
public class ServiceIntegrationMapping
{
    public int Id { get; set; }

    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    public Guid IntegrationId { get; set; }
    public Integration Integration { get; set; } = null!;

    /// <summary>Provider-specific remote coordinates as a JSON blob (see class summary).</summary>
    public string MappingJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

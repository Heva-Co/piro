namespace Piro.Domain.Entities;

public class Integration
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The integration's stable id (RFC 0016) — the string discriminator that replaced the
    /// <c>IntegrationType</c> enum. Equals the integration's <c>IIntegration.IntegrationId</c>
    /// ("Jira", "Twilio", …). Persisted as this string, unchanged from the former enum-name storage.
    /// </summary>
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>JSON blob with provider-specific credentials (e.g. Google service account JSON).</summary>
    public string ConfigJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// On-call escalation policy for alerts produced by this Integration with no Service to
    /// inherit one from (orphan alerts — RFC 0001 §4.3/§4.6). Same nullable-FK shape as
    /// <see cref="Service.EscalationPolicyId"/>.
    /// </summary>
    public int? EscalationPolicyId { get; set; }
    public EscalationPolicy? EscalationPolicy { get; set; }

    public ICollection<Check> Checks { get; set; } = [];
}

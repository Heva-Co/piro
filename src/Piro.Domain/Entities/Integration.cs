using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

public class Integration
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IntegrationType Type { get; set; }
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

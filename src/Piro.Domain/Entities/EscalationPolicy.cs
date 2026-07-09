namespace Piro.Domain.Entities;

public class EscalationPolicy
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Minutes after ACK before re-escalating if incident still unresolved. 0 = disabled.</summary>
    public int ReEscalateAfterAckMinutes { get; set; }
    /// <summary>Minutes of no human activity before re-escalating. 0 = disabled.</summary>
    public int ReEscalateAfterInactivityMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<EscalationStep> Steps { get; set; } = [];
}

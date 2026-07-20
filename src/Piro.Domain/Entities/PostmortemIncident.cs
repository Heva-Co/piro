namespace Piro.Domain.Entities;

/// <summary>
/// Junction between a <see cref="Postmortem"/> and an <see cref="Incident"/> it reviews (RFC 0005 §4.6).
/// N:M — one report may cover several correlated incidents. Modeled on <see cref="IncidentService"/>.
/// Referencing an incident leaves the incident entirely untouched.
/// </summary>
public class PostmortemIncident
{
    public int PostmortemId { get; set; }
    public int IncidentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Postmortem Postmortem { get; set; } = null!;
    public Incident Incident { get; set; } = null!;
}

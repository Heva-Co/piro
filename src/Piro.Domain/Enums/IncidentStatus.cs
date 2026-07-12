namespace Piro.Domain.Enums;

/// <summary>Current investigation status of an incident.</summary>
public enum IncidentStatus
{
    Investigating,
    Identified,
    Monitoring,
    Resolved,

    /// <summary>Absorbed into another incident by automatic correlation. A final state like Resolved — its
    /// timeline and visibility now live on the target incident.</summary>
    Merged
}

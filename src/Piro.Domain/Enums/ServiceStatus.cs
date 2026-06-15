namespace Piro.Domain.Enums;

/// <summary>Operational status of a service or check.</summary>
/// <remarks>Priority order (highest first): MAINTENANCE > DOWN > DEGRADED > UP > NO_DATA.</remarks>
public enum ServiceStatus
{
    NO_DATA,
    UP,
    DEGRADED,
    DOWN,
    MAINTENANCE
}

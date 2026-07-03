namespace Piro.Domain.Enums;

/// <summary>How critical a check is to its parent service. Used to derive incident impact when auto-creating incidents.</summary>
public enum CheckCriticality
{
    /// <summary>Service is fully down when this check fails. Maps to <see cref="ServiceStatus.DOWN"/> impact.</summary>
    Critical,

    /// <summary>Service is degraded when this check fails. Maps to <see cref="ServiceStatus.DEGRADED"/> impact.</summary>
    High,

    /// <summary>Service performance is reduced. Maps to <see cref="ServiceStatus.DEGRADED"/> impact.</summary>
    Medium,

    /// <summary>Non-critical path. Maps to <see cref="ServiceStatus.DEGRADED"/> impact.</summary>
    Low
}

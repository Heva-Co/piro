using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>Persisted cache of a service's computed status at a point in time.</summary>
/// <remarks>
/// Written by <c>ServiceStatusService</c> after every status recomputation.
/// Used by the public status page to avoid re-running the propagation algorithm on each request.
/// </remarks>
public class ServiceStatusSnapshot
{
    public int ServiceId { get; set; }

    /// <summary>Unix timestamp in seconds, aligned to the start of the minute.</summary>
    public long Timestamp { get; set; }

    public ServiceStatus ComputedStatus { get; set; }

    /// <summary>Comma-separated slugs of upstream services that caused a cascaded status.</summary>
    public string? PropagationSources { get; set; }

    public Service Service { get; set; } = null!;
}

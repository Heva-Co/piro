using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>Directed dependency edge between two services in the status propagation DAG.</summary>
/// <remarks>
/// When <see cref="PropagationMode"/> is <see cref="DependencyPropagationMode.Blocking"/>,
/// a DOWN or DEGRADED status on <see cref="DependsOnService"/> cascades to <see cref="Service"/>.
/// Self-loops and cycles are rejected at write time.
/// </remarks>
public class ServiceDependency
{
    /// <summary>The dependent service (the one that inherits status from upstream).</summary>
    public int ServiceId { get; set; }

    /// <summary>The upstream service being depended on.</summary>
    public int DependsOnServiceId { get; set; }

    public DependencyPropagationMode PropagationMode { get; set; } = DependencyPropagationMode.Blocking;

    public DateTime CreatedAt { get; set; }

    public Service Service { get; set; } = null!;
    public Service DependsOnService { get; set; } = null!;
}

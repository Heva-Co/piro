namespace Piro.Domain.Enums;

/// <summary>Controls whether a dependency's degraded status cascades to the dependent service.</summary>
public enum DependencyPropagationMode
{
    /// <summary>A DOWN or DEGRADED upstream forces this service to the exact same status.</summary>
    Blocking,

    /// <summary>
    /// A DOWN or DEGRADED upstream forces this service to at most DEGRADED.
    /// Useful for non-critical dependencies where full DOWN propagation is too aggressive.
    /// </summary>
    SoftBlocking,

    /// <summary>The dependency is visible in the UI but does not affect the computed status.</summary>
    Advisory
}

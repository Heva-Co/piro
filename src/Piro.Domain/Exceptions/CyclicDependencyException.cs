namespace Piro.Domain.Exceptions;

/// <summary>Thrown when adding a service dependency would create a cycle in the DAG.</summary>
public class CyclicDependencyException(string serviceSlug, string dependsOnSlug)
    : Exception($"Adding dependency '{serviceSlug} → {dependsOnSlug}' would create a cycle.")
{
    public string ServiceSlug { get; } = serviceSlug;
    public string DependsOnSlug { get; } = dependsOnSlug;
}

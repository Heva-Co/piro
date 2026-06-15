namespace Piro.Domain.Exceptions;

/// <summary>Thrown when a requested resource does not exist.</summary>
public class NotFoundException(string resourceName, object key)
    : Exception($"{resourceName} '{key}' was not found.")
{
    public string ResourceName { get; } = resourceName;
    public object Key { get; } = key;
}

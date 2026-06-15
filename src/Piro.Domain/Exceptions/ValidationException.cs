namespace Piro.Domain.Exceptions;

/// <summary>Thrown when a command or entity fails domain validation rules.</summary>
public class DomainValidationException(string message) : Exception(message);

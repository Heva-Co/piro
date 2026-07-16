namespace Piro.Domain.Attributes;

/// <summary>
/// Marks a property on an Integration config class (see <see cref="IntegrationManifestAttribute"/>)
/// as holding a credential that must be masked before leaving the server and never logged in plaintext.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SecretFieldAttribute : Attribute;

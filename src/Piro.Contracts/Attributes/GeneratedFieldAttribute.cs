namespace Piro.Contracts;

/// <summary>
/// Marks a property on an Integration config class (see <see cref="IntegrationManifestAttribute"/>)
/// as server-generated, never user-supplied — e.g. a webhook auth token created on Integration
/// creation (see <c>IntegrationAppService.InjectAuthTokenIfNeeded</c>). The admin form skips this
/// field's own input entirely: nothing to type before creation, read-only display after.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class GeneratedFieldAttribute : Attribute;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Piro.Application.DTOs;
using Piro.Domain.Attributes;
using Piro.Domain.Enums;
using Piro.Domain.Extensions;

namespace Piro.Application.Extensions;

/// <summary>
/// Builds the wire-level <see cref="IntegrationTypeMetaDto"/> for an IntegrationType by reflecting
/// over its manifest ConfigType (see IntegrationManifestAttribute) — the schema is derived, never
/// hand-authored, so it can't drift from what the code actually deserializes.
/// </summary>
public static class IntegrationManifestExtensions
{
    private static readonly JsonNamingPolicy ConfigJsonNaming = JsonNamingPolicy.CamelCase;

    /// <summary>
    /// Reflected ConfigFieldSchemaDto[] per ConfigType, cached since the shape of a given manifest
    /// ConfigType never changes at runtime — see RFC 0003 §8 (reflection cost).
    /// </summary>
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<ConfigFieldSchemaDto>> SchemaCache = new();

    /// <summary>
    /// Returns the wire-level manifest for this type, or null for a type with none (e.g. the
    /// obsolete ones).
    /// </summary>
    public static IntegrationTypeMetaDto? ToMetaDto(this IntegrationType type)
    {
        var manifest = type.GetManifest();
        if (manifest is null)
            return null;

        return new IntegrationTypeMetaDto(
            type.ToString(),
            manifest.Label,
            manifest.Description,
            manifest.IconifyIcon,
            manifest.Category,
            manifest.ChannelOnly,
            manifest.Creatable,
            manifest.Direction,
            CapabilityNames(manifest.Capabilities),
            SchemaCache.GetOrAdd(manifest.ConfigType, BuildConfigSchema),
            manifest.WebhookPath
        );
    }

    /// <summary>
    /// Expands a <see cref="IntegrationCapability"/> flag set into its individual flag names,
    /// excluding <see cref="IntegrationCapability.None"/>.
    /// </summary>
    private static IReadOnlyList<string> CapabilityNames(IntegrationCapability capabilities) =>
        Enum.GetValues<IntegrationCapability>()
            .Where(c => c != IntegrationCapability.None && capabilities.HasFlag(c))
            .Select(c => c.ToString())
            .ToArray();

    /// <summary>
    /// Reflects over a manifest ConfigType's public instance properties to build its
    /// <see cref="ConfigFieldSchemaDto"/> list — the derivation step that keeps the wire schema
    /// from drifting out of sync with what the type actually deserializes.
    /// </summary>
    private static IReadOnlyList<ConfigFieldSchemaDto> BuildConfigSchema(Type configType) =>
        configType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(BuildFieldSchema)
            .ToArray();

    /// <summary>
    /// Builds a single property's <see cref="ConfigFieldSchemaDto"/> — label/placeholder/help text
    /// come from <see cref="ConfigFieldAttribute"/> (falling back to the property name as label),
    /// options from <see cref="ConfigFieldOptionsAttribute"/>, and Type/Required/IsSecret from the
    /// Data Annotations already used for validation and masking.
    /// </summary>
    private static ConfigFieldSchemaDto BuildFieldSchema(PropertyInfo property)
    {
        var display = property.GetCustomAttribute<ConfigFieldAttribute>();
        var options = property.GetCustomAttribute<ConfigFieldOptionsAttribute>()?.Options;

        return new ConfigFieldSchemaDto(
            ConfigJsonNaming.ConvertName(property.Name),
            display?.Label ?? property.Name,
            InferFieldType(property, options),
            property.GetCustomAttribute<RequiredAttribute>() is not null,
            property.GetCustomAttribute<SecretFieldAttribute>() is not null,
            property.GetCustomAttribute<SupportsFileUploadAttribute>() is not null,
            display?.Placeholder,
            display?.HelpText,
            options,
            property.GetCustomAttribute<GeneratedFieldAttribute>() is not null
        );
    }

    /// <summary>
    /// Derives a property's <see cref="ConfigFieldType"/> — an explicit <see cref="ConfigFieldOptionsAttribute"/>
    /// wins as <see cref="ConfigFieldType.Enum"/>, then <see cref="MultilineFieldAttribute"/> as
    /// <see cref="ConfigFieldType.Multiline"/>, then <see cref="UrlAttribute"/>/
    /// <see cref="EmailAddressAttribute"/>, defaulting to <see cref="ConfigFieldType.String"/>.
    /// Orthogonal to whether the field is secret — see <see cref="SecretFieldAttribute"/> and
    /// <see cref="ConfigFieldSchemaDto.IsSecret"/>.
    /// </summary>
    private static ConfigFieldType InferFieldType(PropertyInfo property, string[]? options)
    {
        if (options is { Length: > 0 })
            return ConfigFieldType.Enum;
        if (property.GetCustomAttribute<MultilineFieldAttribute>() is not null)
            return ConfigFieldType.Multiline;
        if (property.GetCustomAttribute<UrlAttribute>() is not null)
            return ConfigFieldType.Url;
        if (property.GetCustomAttribute<EmailAddressAttribute>() is not null)
            return ConfigFieldType.Email;
        return ConfigFieldType.String;
    }
}

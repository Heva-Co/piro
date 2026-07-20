using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Piro.Application.DTOs;
using Piro.Domain.Attributes;
using Piro.Domain.Enums;

namespace Piro.Application.Extensions;

/// <summary>
/// Reflects an annotated config class into its wire-level <see cref="ConfigFieldSchemaDto"/> list.
/// Shared by both Integration manifests (<see cref="IntegrationManifestExtensions"/>) and Check
/// manifests (RFC 0011) — the schema is derived from Data Annotations on the config type's
/// properties, never hand-authored, so it can't drift from what the code actually deserializes.
/// </summary>
public static class ConfigSchemaBuilder
{
    private static readonly JsonNamingPolicy ConfigJsonNaming = JsonNamingPolicy.CamelCase;

    /// <summary>
    /// Reflected <see cref="ConfigFieldSchemaDto"/>[] per config type, cached since the shape of a
    /// given config type never changes at runtime — see RFC 0003 §8 (reflection cost).
    /// </summary>
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<ConfigFieldSchemaDto>> SchemaCache = new();

    /// <summary>
    /// Returns the (cached) reflected field schema for a config type — e.g. an IntegrationType's
    /// manifest ConfigType or a CheckType's *CheckConfig record.
    /// </summary>
    public static IReadOnlyList<ConfigFieldSchemaDto> For(Type configType) =>
        SchemaCache.GetOrAdd(configType, BuildConfigSchema);

    /// <summary>
    /// Reflects over a config type's public instance properties to build its
    /// <see cref="ConfigFieldSchemaDto"/> list — the derivation step that keeps the wire schema
    /// from drifting out of sync with what the type actually deserializes.
    /// </summary>
    private static IReadOnlyList<ConfigFieldSchemaDto> BuildConfigSchema(Type configType)
    {
        // A default instance lets us read each property's initializer value (Method = "GET", etc.)
        // so the schema can carry defaults for the admin to seed a new form. Records/classes with a
        // parameterless ctor instantiate cleanly; anything else falls back to no defaults.
        var defaults = TryCreateDefault(configType);
        return configType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => BuildFieldSchema(p, defaults))
            .ToArray();
    }

    private static object? TryCreateDefault(Type configType)
    {
        try { return Activator.CreateInstance(configType); }
        catch { return null; }
    }

    /// <summary>
    /// Builds a single property's <see cref="ConfigFieldSchemaDto"/> — label/placeholder/help text
    /// come from <see cref="ConfigFieldAttribute"/> (falling back to the property name as label),
    /// options from <see cref="ConfigFieldOptionsAttribute"/>, and Type/Required/IsSecret from the
    /// Data Annotations already used for validation and masking.
    /// </summary>
    private static ConfigFieldSchemaDto BuildFieldSchema(PropertyInfo property, object? defaults)
    {
        var display = property.GetCustomAttribute<ConfigFieldAttribute>();
        var options = property.GetCustomAttribute<ConfigFieldOptionsAttribute>()?.Options;
        var dynamicOptions = property.GetCustomAttribute<DynamicOptionsAttribute>();
        var fieldType = InferFieldType(property, options);

        return new ConfigFieldSchemaDto(
            ConfigJsonNaming.ConvertName(property.Name),
            display?.Label ?? property.Name,
            fieldType,
            property.GetCustomAttribute<RequiredAttribute>() is not null,
            property.GetCustomAttribute<SecretFieldAttribute>() is not null,
            property.GetCustomAttribute<SupportsFileUploadAttribute>() is not null,
            display?.Placeholder,
            display?.HelpText,
            options,
            property.GetCustomAttribute<GeneratedFieldAttribute>() is not null,
            defaults is null ? null : property.GetValue(defaults),
            // For an ObjectArray, recurse into the list element type so the frontend can render
            // a repeater of sub-forms (e.g. HttpResponseRule inside HttpCheckConfig.ResponseRules).
            fieldType == ConfigFieldType.ObjectArray ? For(ElementType(property.PropertyType)!) : null,
            VisibilityFrom(property),
            property.GetCustomAttribute<ConfigValidationAttribute>()?.Validator,
            dynamicOptions?.SourceKey,
            dynamicOptions?.DependsOn is { } dependsOn ? ConfigJsonNaming.ConvertName(dependsOn) : null
        );
    }

    /// <summary>
    /// Derives a property's <see cref="ConfigFieldType"/>. Explicit annotations win in order —
    /// <see cref="ConfigFieldOptionsAttribute"/> → <see cref="ConfigFieldType.Enum"/>,
    /// <see cref="CodeFieldAttribute"/> → <see cref="ConfigFieldType.Code"/>,
    /// <see cref="MultilineFieldAttribute"/> → <see cref="ConfigFieldType.Multiline"/>,
    /// <see cref="UrlAttribute"/>/<see cref="EmailAddressAttribute"/> → Url/Email — then the CLR type
    /// is inspected (bool → Boolean, numeric → Number, List&lt;string&gt; → StringList,
    /// Dictionary&lt;string,string&gt; → KeyValue, List&lt;record&gt; → ObjectArray), defaulting to
    /// <see cref="ConfigFieldType.String"/>. Orthogonal to whether the field is secret.
    /// </summary>
    private static ConfigFieldType InferFieldType(PropertyInfo property, string[]? options)
    {
        if (options is { Length: > 0 })
            return ConfigFieldType.Enum;
        if (property.GetCustomAttribute<CodeFieldAttribute>() is not null)
            return ConfigFieldType.Code;
        if (property.GetCustomAttribute<MarkdownFieldAttribute>() is not null)
            return ConfigFieldType.Markdown;
        if (property.GetCustomAttribute<MultilineFieldAttribute>() is not null)
            return ConfigFieldType.Multiline;
        if (property.GetCustomAttribute<UrlAttribute>() is not null)
            return ConfigFieldType.Url;
        if (property.GetCustomAttribute<EmailAddressAttribute>() is not null)
            return ConfigFieldType.Email;

        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type == typeof(bool))
            return ConfigFieldType.Boolean;
        if (type == typeof(int) || type == typeof(long) || type == typeof(double))
            return ConfigFieldType.Number;
        if (IsDictionaryOfStrings(type))
            return ConfigFieldType.KeyValue;

        var element = ElementType(type);
        if (element == typeof(string))
            return ConfigFieldType.StringList;
        if (element is not null && element.IsClass && element != typeof(string))
            return ConfigFieldType.ObjectArray;

        return ConfigFieldType.String;
    }

    /// <summary>Maps a <see cref="VisibleWhenAttribute"/> to its wire DTO, or null when the field is unconditional.</summary>
    private static ConfigFieldVisibilityDto? VisibilityFrom(PropertyInfo property)
    {
        var attr = property.GetCustomAttribute<VisibleWhenAttribute>();
        return attr is null ? null : new ConfigFieldVisibilityDto(attr.Field, attr.Values);
    }

    /// <summary>True for a <see cref="Dictionary{TKey,TValue}"/> (or IDictionary) with string keys and values.</summary>
    private static bool IsDictionaryOfStrings(Type type)
    {
        if (!type.IsGenericType) return false;
        var def = type.GetGenericTypeDefinition();
        if (def != typeof(Dictionary<,>) && def != typeof(IDictionary<,>)) return false;
        var args = type.GetGenericArguments();
        return args[0] == typeof(string) && args[1] == typeof(string);
    }

    /// <summary>The element type of a <see cref="List{T}"/>/<see cref="IReadOnlyList{T}"/>-style property, or null when the type is not a supported list.</summary>
    private static Type? ElementType(Type type)
    {
        if (!type.IsGenericType) return null;
        var def = type.GetGenericTypeDefinition();
        if (def == typeof(List<>) || def == typeof(IReadOnlyList<>) ||
            def == typeof(IList<>) || def == typeof(ICollection<>) || def == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];
        return null;
    }
}

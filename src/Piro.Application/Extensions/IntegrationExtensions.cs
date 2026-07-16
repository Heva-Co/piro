using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Piro.Application.DTOs;
using Piro.Domain.Attributes;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Extensions;

namespace Piro.Application.Extensions;

public static class IntegrationExtensions
{
    /// <summary>
    /// Sentinel returned in place of a real secret value. Sent back unchanged on update means "keep
    /// the existing secret".
    /// </summary>
    public const string MaskedSecretValue = "__MASKED__";

    private static readonly JsonNamingPolicy ConfigJsonNaming = JsonNamingPolicy.CamelCase;

    /// <summary>
    /// Maps an <see cref="Integration"/> entity to its outbound DTO representation, masking secret
    /// config keys.
    /// </summary>
    public static IntegrationDto ToDto(this Integration i) => new(
        i.Id,
        i.Name,
        i.Type,
        i.Type.GetCategory(),
        i.Description,
        MaskSecrets(i.Type, i.ConfigJson),
        i.Checks.Count,
        i.CreatedAt,
        i.UpdatedAt
    );

    /// <summary>
    /// Replaces any secret-marked config key's value in <paramref name="configJson"/> with
    /// <see cref="MaskedSecretValue"/> before it leaves the server. Secret keys are discovered by
    /// reflecting over the IntegrationType's manifest ConfigType for properties annotated with
    /// <see cref="SecretFieldAttribute"/> — see IntegrationManifestAttribute.
    /// </summary>
    public static string MaskSecrets(IntegrationType type, string configJson)
    {
        var secretKeys = GetSecretKeys(type);
        if (secretKeys.Length == 0)
            return configJson;

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(configJson);
        }
        catch (JsonException)
        {
            return configJson;
        }

        if (node is not JsonObject obj)
            return configJson;

        foreach (var key in secretKeys)
        {
            if (obj[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String && !string.IsNullOrEmpty(value.GetValue<string>()))
                obj[key] = MaskedSecretValue;
        }

        return obj.ToJsonString();
    }

    /// <summary>
    /// The ConfigJson property names (in wire/camelCase form) marked <see cref="SecretFieldAttribute"/>
    /// on this IntegrationType's manifest ConfigType. Empty for a type with no manifest or no secret fields.
    /// </summary>
    public static string[] GetSecretKeys(IntegrationType type)
    {
        var configType = type.GetManifest()?.ConfigType;
        if (configType is null)
            return [];

        return configType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<SecretFieldAttribute>() is not null)
            .Select(p => ConfigJsonNaming.ConvertName(p.Name))
            .ToArray();
    }
}

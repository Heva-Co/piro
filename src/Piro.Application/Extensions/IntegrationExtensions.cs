using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Attributes;
using Piro.Contracts;
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
    public static IntegrationDto ToDto(this Integration i, bool revealGeneratedFields = false) => new(
        i.Id,
        i.Name,
        i.Type,
        i.Type.GetCategory(),
        i.Description,
        MaskSecrets(i.Type, i.ConfigJson, revealGeneratedFields),
        i.Checks.Count,
        i.CreatedAt,
        i.UpdatedAt,
        i.EscalationPolicyId
    );

    /// <summary>
    /// Replaces any secret-marked config key's value in <paramref name="configJson"/> with
    /// <see cref="MaskedSecretValue"/> before it leaves the server. Secret keys are discovered by
    /// reflecting over the IntegrationType's manifest ConfigType for properties annotated with
    /// <see cref="SecretFieldAttribute"/> — see IntegrationManifestAttribute.
    /// <paramref name="revealGeneratedFields"/> skips masking for fields also marked
    /// <see cref="GeneratedFieldAttribute"/> (e.g. a webhook auth token) — set only on the response
    /// to the create call, the one time an admin can see a server-generated secret's real value.
    /// </summary>
    public static string MaskSecrets(IntegrationType type, string configJson, bool revealGeneratedFields = false)
    {
        var secretKeys = GetSecretKeys(type);
        if (secretKeys.Length == 0)
            return configJson;

        var generatedKeys = revealGeneratedFields ? GetGeneratedFieldKeys(type) : [];

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
            if (generatedKeys.Contains(key))
                continue;

            if (obj[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String && !string.IsNullOrEmpty(value.GetValue<string>()))
                obj[key] = MaskedSecretValue;
        }

        return obj.ToJsonString();
    }

    /// <summary>
    /// Encrypts every <see cref="SecretFieldAttribute"/> value in <paramref name="configJson"/> at rest,
    /// discovering which keys are secret by reflection (the same <see cref="GetSecretKeys"/> set masking
    /// uses). Already-protected values and the masked sentinel are left untouched, so re-saving is safe.
    /// Returns the config unchanged if the type has no secret fields.
    /// </summary>
    public static string ProtectSecrets(IntegrationType type, string configJson, ISecretProtector protector) =>
        TransformSecrets(type, configJson, (protector, plaintext: true));

    /// <summary>Reverses <see cref="ProtectSecrets"/> — decrypts protected secret values for in-process use.</summary>
    public static string UnprotectSecrets(IntegrationType type, string configJson, ISecretProtector protector) =>
        TransformSecrets(type, configJson, (protector, plaintext: false));

    /// <summary>
    /// The centralized consumption read: returns this integration's ConfigJson with every secret field
    /// decrypted, ready to deserialize into a consumer's config record. This is the ONLY path a
    /// dispatcher/executor/token-provider should use to read a config it intends to act on — reading
    /// <see cref="Integration.ConfigJson"/> directly yields ciphertext (secrets are encrypted at rest).
    /// Legacy plaintext values pass through unchanged (see <see cref="ISecretProtector.IsProtected"/>),
    /// so this is safe for rows written before encryption was applied. Never used for the outbound DTO,
    /// which masks instead of decrypts (see <see cref="ToDto"/>).
    /// </summary>
    public static string ReadDecryptedConfigJson(this Integration integration, ISecretProtector protector)
    {
        return UnprotectSecrets(integration.Type, integration.ConfigJson, protector);
    }

    private static string TransformSecrets(IntegrationType type, string configJson, (ISecretProtector Protector, bool Protecting) op)
    {
        var secretKeys = GetSecretKeys(type);
        if (secretKeys.Length == 0)
            return configJson;

        JsonNode? node;
        try { node = JsonNode.Parse(configJson); }
        catch (JsonException) { return configJson; }

        if (node is not JsonObject obj)
            return configJson;

        foreach (var key in secretKeys)
        {
            if (obj[key] is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
                continue;
            var current = value.GetValue<string>();
            if (string.IsNullOrEmpty(current) || current == MaskedSecretValue)
                continue;

            if (op.Protecting)
            {
                if (!op.Protector.IsProtected(current))
                    obj[key] = op.Protector.Protect(current);
            }
            else
            {
                if (op.Protector.IsProtected(current))
                    obj[key] = op.Protector.Unprotect(current);
            }
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

    /// <summary>The ConfigJson property names marked <see cref="GeneratedFieldAttribute"/> on this IntegrationType's manifest ConfigType.</summary>
    private static string[] GetGeneratedFieldKeys(IntegrationType type)
    {
        var configType = type.GetManifest()?.ConfigType;
        if (configType is null)
            return [];

        return configType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<GeneratedFieldAttribute>() is not null)
            .Select(p => ConfigJsonNaming.ConvertName(p.Name))
            .ToArray();
    }
}

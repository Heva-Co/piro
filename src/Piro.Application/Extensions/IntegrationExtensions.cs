using System.Text.Json;
using System.Text.Json.Nodes;
using Piro.Application.DTOs;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Extensions;

namespace Piro.Application.Extensions;

public static class IntegrationExtensions
{
    /// <summary>Sentinel returned in place of a real secret value. Sent back unchanged on update means "keep the existing secret".</summary>
    public const string MaskedSecretValue = "__MASKED__";

    /// <summary>JSON keys within each integration type's ConfigJson that hold credentials and must never be sent to the client in plaintext.</summary>
    public static readonly Dictionary<IntegrationType, string[]> SecretKeysByType = new()
    {
        [IntegrationType.GoogleCloud] = ["serviceAccountJson"],
        [IntegrationType.Jira] = ["apiToken"],
        [IntegrationType.PagerDuty] = ["routingKey"],
        [IntegrationType.MSTeams] = ["webhookUrl"],
        [IntegrationType.Telegram] = ["botToken"],
        [IntegrationType.Twilio] = ["authToken"],
        [IntegrationType.Opsgenie] = ["apiKey"],
        [IntegrationType.Pushover] = ["appToken"],
        [IntegrationType.Ntfy] = ["token"],
    };

    /// <summary>Maps an <see cref="Integration"/> entity to its outbound DTO representation, masking secret config keys.</summary>
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

    /// <summary>Replaces any known secret key's value in <paramref name="configJson"/> with <see cref="MaskedSecretValue"/> before it leaves the server.</summary>
    public static string MaskSecrets(IntegrationType type, string configJson)
    {
        if (!SecretKeysByType.TryGetValue(type, out var secretKeys))
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
}

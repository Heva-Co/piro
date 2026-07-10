using System.Text.Json;
using System.Text.Json.Nodes;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;
using Piro.Domain.Extensions;

namespace Piro.Application.Services;

public class IntegrationAppService(IIntegrationRepository repository)
{
    /// <summary>Sentinel returned in place of a real secret value. Sent back unchanged on update means "keep the existing secret".</summary>
    public const string MaskedSecretValue = "__MASKED__";

    /// <summary>JSON keys within each integration type's ConfigJson that hold credentials and must never be sent to the client in plaintext.</summary>
    private static readonly Dictionary<IntegrationType, string[]> SecretKeysByType = new()
    {
        [IntegrationType.GoogleCloud] = ["serviceAccountJson"],
        [IntegrationType.Jira] = ["apiToken"],
        [IntegrationType.PagerDuty] = ["routingKey"],
        [IntegrationType.MSTeams] = ["webhookUrl"],
        [IntegrationType.Telegram] = ["botToken"],
        [IntegrationType.TwilioSms] = ["authToken"],
        [IntegrationType.Opsgenie] = ["apiKey"],
        [IntegrationType.Pushover] = ["appToken"],
        [IntegrationType.Ntfy] = ["token"],
    };

    public async Task<IEnumerable<IntegrationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await repository.GetAllAsync(ct);
        return items.Select(ToDto);
    }

    public async Task<IntegrationDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var item = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Integration), id.ToString());
        return ToDto(item);
    }

    public async Task<IntegrationDto> CreateAsync(CreateIntegrationRequest request, CancellationToken ct = default)
    {
        var integration = new Integration
        {
            Name = request.Name,
            Type = request.Type,
            Description = request.Description,
            ConfigJson = request.ConfigJson
        };
        var created = await repository.CreateAsync(integration, ct);
        return ToDto(created);
    }

    public async Task<IntegrationDto> UpdateAsync(int id, UpdateIntegrationRequest request, CancellationToken ct = default)
    {
        var integration = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Integration), id.ToString());

        if (request.Name is not null) integration.Name = request.Name;
        if (request.Description is not null) integration.Description = request.Description;
        if (request.ConfigJson is not null)
            integration.ConfigJson = MergeConfigJson(integration.Type, integration.ConfigJson, request.ConfigJson);

        var updated = await repository.UpdateAsync(integration, ct);
        return ToDto(updated);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var integration = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Integration), id.ToString());

        if (integration.Checks.Count > 0)
            throw new DomainValidationException(
                $"Integration '{integration.Name}' is referenced by {integration.Checks.Count} check(s). Remove or reassign those checks before deleting.");

        await repository.DeleteAsync(integration, ct);
    }

    /// <summary>Replaces any known secret key's value in <paramref name="configJson"/> with <see cref="MaskedSecretValue"/> before it leaves the server.</summary>
    private static string MaskSecrets(IntegrationType type, string configJson)
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

    /// <summary>
    /// Merges an incoming ConfigJson over the existing one: any secret key whose incoming value is
    /// still the masked sentinel is left untouched, so a client re-submitting a masked form (without
    /// the user having entered a new secret) doesn't overwrite the stored credential with the placeholder.
    /// </summary>
    private static string MergeConfigJson(IntegrationType type, string existingConfigJson, string incomingConfigJson)
    {
        if (!SecretKeysByType.TryGetValue(type, out var secretKeys))
            return incomingConfigJson;

        JsonNode? incomingNode;
        try
        {
            incomingNode = JsonNode.Parse(incomingConfigJson);
        }
        catch (JsonException)
        {
            return incomingConfigJson;
        }

        if (incomingNode is not JsonObject incoming)
            return incomingConfigJson;

        JsonObject? existing = null;
        try
        {
            existing = JsonNode.Parse(existingConfigJson) as JsonObject;
        }
        catch (JsonException) { /* keep existing == null */ }

        foreach (var key in secretKeys)
        {
            if (incoming[key] is JsonValue value &&
                value.GetValueKind() == JsonValueKind.String &&
                value.GetValue<string>() == MaskedSecretValue)
            {
                incoming[key] = existing?[key]?.DeepClone();
            }
        }

        return incoming.ToJsonString();
    }

    private static IntegrationDto ToDto(Integration i) => new(
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
}

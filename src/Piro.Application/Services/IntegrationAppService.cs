using System.Text.Json;
using System.Text.Json.Nodes;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

public class IntegrationAppService(IIntegrationRepository repository)
{
    /// <summary>Sentinel returned in place of a real secret value. Sent back unchanged on update means "keep the existing secret".</summary>
    public const string MaskedSecretValue = IntegrationExtensions.MaskedSecretValue;

    public async Task<IEnumerable<IntegrationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await repository.GetAllAsync(ct);
        return items.Select(i => i.ToDto());
    }

    public async Task<IntegrationDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var item = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Integration), id.ToString());
        return item.ToDto();
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
        return created.ToDto();
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
        return updated.ToDto();
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

    /// <summary>
    /// Merges an incoming ConfigJson over the existing one: any secret key whose incoming value is
    /// still the masked sentinel is left untouched, so a client re-submitting a masked form (without
    /// the user having entered a new secret) doesn't overwrite the stored credential with the placeholder.
    /// </summary>
    private static string MergeConfigJson(IntegrationType type, string existingConfigJson, string incomingConfigJson)
    {
        if (!IntegrationExtensions.SecretKeysByType.TryGetValue(type, out var secretKeys))
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
}

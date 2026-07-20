using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Integrations.Actions;
using Piro.Application.Interfaces;
using Piro.Domain.Attributes;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

public class IntegrationAppService(
    IIntegrationRepository repository,
    IWebhookRequestLogRepository webhookLogRepository,
    IEscalationPolicyRepository escalationPolicyRepository,
    ISecretProtector secretProtector,
    IActionHost actionHost,
    IActionRegistry actionRegistry,
    IEnumerable<IOptionsProvider> optionsProviders)
{
    /// <summary>
    /// Resolves the runtime options for a <c>[DynamicOptions]</c> field (RFC 0012): finds the
    /// IOptionsProvider registered for (integration type, sourceKey) and asks it, passing the cascade
    /// parent's value when present. 404 if no provider matches.
    /// </summary>
    public async Task<IReadOnlyList<OptionItem>> GetFieldOptionsAsync(
        Guid integrationId, string sourceKey, string? dependsOn, CancellationToken ct = default)
    {
        var integration = await repository.GetByIdAsync(integrationId, ct)
            ?? throw new NotFoundException(nameof(Integration), integrationId.ToString());

        var provider = optionsProviders.FirstOrDefault(p => p.Type == integration.Type && p.SourceKey == sourceKey)
            ?? throw new NotFoundException("OptionsProvider", $"{integration.Type}/{sourceKey}");

        return await provider.GetOptionsAsync(actionHost, integrationId, dependsOn, ct);
    }

    /// <summary>
    /// Discovers which action buttons to render for an object of the given <paramref name="context"/>
    /// (RFC 0012 §4.4): for each configured integration, take its registered actions whose Contexts
    /// include the context and that are ready to run, and project each to a descriptor. A not-ready
    /// action is dropped entirely — the frontend never receives a descriptor it can't use.
    /// </summary>
    public async Task<IReadOnlyList<IntegrationActionDescriptorDto>> GetActionsAsync(
        ActionContext context, CancellationToken ct = default)
    {
        var integrations = await repository.GetAllAsync(ct);
        var descriptors = new List<IntegrationActionDescriptorDto>();

        foreach (var integration in integrations)
        {
            var label = integration.Type.GetManifest()?.Label ?? integration.Name;

            foreach (var action in actionRegistry.GetActions(integration.Type))
            {
                if (!action.Descriptor.Contexts.Contains(context))
                    continue;
                if (!await action.Handler.IsReadyAsync(actionHost, integration.Id, ct))
                    continue;

                var inputSchema = action.Descriptor.HasInput && action.Handler.InputType is not null
                    ? ConfigSchemaBuilder.For(action.Handler.InputType)
                    : [];

                descriptors.Add(new IntegrationActionDescriptorDto(
                    integration.Id,
                    label,
                    action.Descriptor.ActionId,
                    action.Descriptor.Label,
                    action.Descriptor.Description,
                    action.Descriptor.IconifyIcon,
                    action.Descriptor.HasInput,
                    action.Descriptor.SupportsDraft,
                    inputSchema));
            }
        }

        return descriptors;
    }

    /// <summary>
    /// Builds a pre-filled draft input for an action + target (RFC 0012 §4.6), shaped like the action's
    /// InputType so the dialog round-trips. Null if the action doesn't support drafts or the target is gone.
    /// </summary>
    public async Task<object?> BuildActionDraftAsync(
        Guid integrationId, string actionId, ActionContext context, int targetId, CancellationToken ct = default)
    {
        var integration = await repository.GetByIdAsync(integrationId, ct)
            ?? throw new NotFoundException(nameof(Integration), integrationId.ToString());

        var action = actionRegistry.Resolve(integration.Type, actionId)
            ?? throw new NotFoundException("IntegrationAction", $"{integration.Type}/{actionId}");

        if (!action.Descriptor.SupportsDraft)
            return null;

        var ctx = new ActionExecutionContext(integrationId, context, targetId, Input: null);
        return await action.Handler.BuildDraftAsync(actionHost, ctx, ct);
    }

    /// <summary>
    /// Executes a user-initiated integration action (RFC 0012 §4.4): resolve the action, deserialize and
    /// validate the input against the same DataAnnotations that drove the dialog, run it, and persist the
    /// external reference it created (via the host). Returns that reference to the client.
    /// </summary>
    public async Task<IntegrationActionResultDto> ExecuteActionAsync(
        Guid integrationId, string actionId, ExecuteIntegrationActionRequest request, CancellationToken ct = default)
    {
        var integration = await repository.GetByIdAsync(integrationId, ct)
            ?? throw new NotFoundException(nameof(Integration), integrationId.ToString());

        var action = actionRegistry.Resolve(integration.Type, actionId)
            ?? throw new NotFoundException("IntegrationAction", $"{integration.Type}/{actionId}");

        object? input = null;
        if (action.Handler.InputType is not null)
        {
            if (request.Input is not { } rawInput)
                throw new DomainValidationException($"Action '{actionId}' requires input.");

            input = rawInput.Deserialize(action.Handler.InputType, JsonSerializerOptions.Web)
                ?? throw new DomainValidationException($"Action '{actionId}' input could not be parsed.");

            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true))
                throw new DomainValidationException(
                    string.Join("; ", results.Select(r => r.ErrorMessage)));
        }

        var ctx = new ActionExecutionContext(integrationId, request.Context, request.TargetId, input);
        var result = await action.Handler.ExecuteAsync(actionHost, ctx, ct);

        await actionHost.LinkExternalAsync(
            new ExternalReferenceRequest(
                request.Context, request.TargetId, integrationId, actionId,
                result.ExternalId, result.Url, result.Label, result.Metadata),
            ct);

        return new IntegrationActionResultDto(result.ExternalId, result.Url, result.Label);
    }

    /// <summary>
    /// Returns the outbound external references an integration action has created for a local object
    /// (RFC 0012 §4.5) — read through the <see cref="IActionHost"/>, the same seam actions write
    /// through, so this endpoint never touches the ExternalReferences table directly.
    /// </summary>
    public async Task<IReadOnlyList<ExternalReferenceDto>> GetReferencesAsync(
        ActionContext context, int targetId, CancellationToken ct = default)
    {
        var links = await actionHost.GetLinksAsync(context, targetId, ct);
        return links
            .Select(l => new ExternalReferenceDto(
                l.Context, l.TargetId, l.IntegrationId, l.ActionId, l.ExternalId, l.Url, l.Label, l.Metadata))
            .ToList();
    }

    /// <summary>
    /// Encrypts every <see cref="SecretFieldAttribute"/> value in the config at rest, for every
    /// integration type. <see cref="IntegrationExtensions.ProtectSecrets"/> is a no-op for a type
    /// with no secret fields and idempotent for already-protected values, so this applies
    /// unconditionally — legacy plaintext rows migrate lazily on their next save. Consumers that
    /// actually need a secret decrypt it via <see cref="IntegrationExtensions.UnprotectSecrets"/>;
    /// the outbound DTO keeps masking (never decrypting) untouched.
    /// </summary>
    private string ProtectSecretsIfNeeded(IntegrationType type, string configJson)
    {
        return IntegrationExtensions.ProtectSecrets(type, configJson, secretProtector);
    }

    /// <summary>Sentinel returned in place of a real secret value. Sent back unchanged on update means "keep the existing secret".</summary>
    public const string MaskedSecretValue = IntegrationExtensions.MaskedSecretValue;

    public async Task<IEnumerable<IntegrationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await repository.GetAllAsync(ct);
        return items.Select(i => i.ToDto());
    }

    public async Task<IntegrationDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Integration), id.ToString());
        return item.ToDto();
    }

    public async Task<IntegrationDto> CreateAsync(CreateIntegrationRequest request, CancellationToken ct = default)
    {
        if (request.EscalationPolicyId is int createPolicyId)
            _ = await escalationPolicyRepository.GetByIdAsync(createPolicyId, ct)
                ?? throw new NotFoundException(nameof(EscalationPolicy), createPolicyId.ToString());

        var integration = new Integration
        {
            Name = request.Name,
            Type = request.Type,
            Description = request.Description,
            ConfigJson = ProtectSecretsIfNeeded(request.Type, InjectAuthTokenIfNeeded(request.Type, request.ConfigJson)),
            EscalationPolicyId = request.EscalationPolicyId
        };
        var created = await repository.CreateAsync(integration, ct);
        return created.ToDto(revealGeneratedFields: true);
    }

    /// <summary>
    /// For any inbound-webhook IntegrationType (<see cref="IntegrationCapability.CreatesAlerts"/>),
    /// the client never supplies <c>authToken</c> — it's generated here and written straight into
    /// ConfigJson, so it's masked like every other secret from the moment it first leaves the server.
    /// Not GCP-specific: any future webhook-capable type gets the same treatment automatically.
    /// </summary>
    private static string InjectAuthTokenIfNeeded(IntegrationType type, string configJson)
    {
        var manifest = type.GetManifest();
        if (manifest is null || !manifest.Capabilities.HasFlag(IntegrationCapability.CreatesAlerts))
            return configJson;

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(configJson);
        }
        catch (JsonException)
        {
            node = new JsonObject();
        }

        if (node is not JsonObject obj)
            obj = new JsonObject();
        else
            obj = node.AsObject();

        obj["authToken"] = GenerateAuthToken();
        return obj.ToJsonString();
    }

    private static string GenerateAuthToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

    /// <summary>
    /// Regenerates every server-generated config field for this Integration (e.g. a webhook auth
    /// token lost by the admin, or rotated on suspicion of leak) — invalidates the old value
    /// immediately. Not GCP-specific: any manifest field marked <see cref="GeneratedFieldAttribute"/>
    /// is replaced, whatever the IntegrationType. Returns the new value unmasked, the one time it's visible.
    /// </summary>
    public async Task<IntegrationDto> RegenerateGeneratedFieldsAsync(Guid id, CancellationToken ct = default)
    {
        var integration = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Integration), id.ToString());

        var manifest = integration.Type.GetManifest();
        if (manifest is null || !manifest.Capabilities.HasFlag(IntegrationCapability.CreatesAlerts))
            throw new DomainValidationException($"Integration type '{integration.Type}' has no server-generated fields to regenerate.");

        integration.ConfigJson = InjectAuthTokenIfNeeded(integration.Type, integration.ConfigJson);
        var updated = await repository.UpdateAsync(integration, ct);
        return updated.ToDto(revealGeneratedFields: true);
    }

    public async Task<IntegrationDto> UpdateAsync(Guid id, UpdateIntegrationRequest request, CancellationToken ct = default)
    {
        var integration = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Integration), id.ToString());

        if (request.Name is not null) integration.Name = request.Name;
        if (request.Description is not null) integration.Description = request.Description;
        if (request.ConfigJson is not null)
            integration.ConfigJson = ProtectSecretsIfNeeded(
                integration.Type,
                MergeConfigJson(integration.Type, integration.ConfigJson, request.ConfigJson));

        // Always applies (unlike the fields above) — the admin form always has an opinion on this,
        // same convention as Service.EscalationPolicyId: null explicitly clears the assignment.
        if (request.EscalationPolicyId is int updatePolicyId)
        {
            _ = await escalationPolicyRepository.GetByIdAsync(updatePolicyId, ct)
                ?? throw new NotFoundException(nameof(EscalationPolicy), updatePolicyId.ToString());
            integration.EscalationPolicyId = updatePolicyId;
        }
        else
        {
            integration.EscalationPolicyId = null;
        }

        var updated = await repository.UpdateAsync(integration, ct);
        return updated.ToDto();
    }

    /// <summary>Returns the most recent inbound webhook requests for this Integration — RFC 0001 §4.4. Empty for a non-webhook type.</summary>
    public async Task<IEnumerable<WebhookRequestLogDto>> GetWebhookLogsAsync(Guid id, CancellationToken ct = default)
    {
        var logs = await webhookLogRepository.GetRecentByIntegrationIdAsync(id, limit: 50, ct);
        return logs.Select(l => new WebhookRequestLogDto(l.Id, l.ReceivedAt, l.RawPayload, l.Outcome, l.AlertId));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
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
        var secretKeys = IntegrationExtensions.GetSecretKeys(type);
        if (secretKeys.Length == 0)
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

using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>
/// Wire representation of an IntegrationType's manifest — see IntegrationManifestAttribute.
/// </summary>
public record IntegrationTypeMetaDto(
    string Type,
    string? Label,
    string? Description,
    string? IconifyIcon,
    IntegrationCategory Category,
    bool ChannelOnly,
    bool Creatable,
    IntegrationDirection Direction,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<ConfigFieldSchemaDto> ConfigSchema,
    /// <summary>Path segment under <c>/api/v1/webhooks/</c> this type's inbound endpoint listens on — see IntegrationManifestAttribute.WebhookPath. Null for a non-webhook type.</summary>
    string? WebhookPath
);

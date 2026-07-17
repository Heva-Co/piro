using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>
/// Wire representation of a single ConfigJson property, reflected from an IntegrationType's
/// manifest ConfigType (see IntegrationManifestAttribute) — never hand-authored.
/// </summary>
public record ConfigFieldSchemaDto(
    string Key,
    string Label,
    ConfigFieldType Type,
    bool Required,
    bool IsSecret,
    bool SupportsFileUpload,
    string? Placeholder,
    string? HelpText,
    IReadOnlyList<string>? Options,
    /// <summary>True for a field the server generates itself (e.g. a webhook auth token) — see GeneratedFieldAttribute. The admin form never renders an input for it before creation.</summary>
    bool IsGenerated
);

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

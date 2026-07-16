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
    IReadOnlyList<string>? Options
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
    IReadOnlyList<ConfigFieldSchemaDto> ConfigSchema
);

using Piro.Contracts;
using Piro.Integrations.Abstractions;

namespace Piro.Application.DTOs;

/// <summary>
/// Wire representation of an integration type's manifest, projected from the integration's own
/// <see cref="Piro.Integrations.Abstractions.IIntegration"/> class (RFC 0016).
/// </summary>
public record IntegrationTypeMetaDto(
    string Type,
    string? Label,
    string? Description,
    string? IconifyIcon,
    bool ChannelOnly,
    bool Creatable,
    IntegrationDirection Direction,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<ConfigFieldSchemaDto> ConfigSchema
);

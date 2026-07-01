using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

public record IntegrationDto(
    int Id,
    string Name,
    IntegrationType Type,
    string? Description,
    string ConfigJson,
    int CheckCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateIntegrationRequest(
    string Name,
    IntegrationType Type,
    string? Description,
    string ConfigJson
);

public record UpdateIntegrationRequest(
    string? Name,
    string? Description,
    string? ConfigJson
);

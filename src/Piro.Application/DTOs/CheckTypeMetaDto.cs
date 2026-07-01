using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

public record CheckTypeMetaDto(
    string Type,
    string? RequiredIntegrationType
);

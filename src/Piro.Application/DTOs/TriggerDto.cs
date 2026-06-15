using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

public record TriggerDto(
    int Id,
    string Name,
    TriggerType Type,
    string? Description,
    string? Status,
    string MetaJson,
    bool IsGlobal,
    bool IsLocked,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int AlertConfigCount = 0
);

public record CreateTriggerRequest(
    string Name,
    TriggerType Type,
    string? Description = null,
    string MetaJson = "{}",
    bool IsGlobal = false,
    bool IsLocked = false
);

public record UpdateTriggerRequest(
    string? Name,
    string? Description,
    string? Status,
    string? MetaJson,
    bool? IsGlobal = null,
    bool? IsLocked = null
);

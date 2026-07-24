using Piro.Domain.Enums;
using Piro.Integrations.Abstractions;

namespace Piro.Application.DTOs;

public record IntegrationDto(
    Guid Id,
    string Name,
    string Type,
    string? Description,
    string ConfigJson,
    int CheckCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    /// <summary>On-call policy for alerts from this Integration with no Service to inherit one from — meaningful only when the type's manifest has IntegrationCapability.SupportsEscalationPolicy.</summary>
    int? EscalationPolicyId
);

public record CreateIntegrationRequest(
    string Name,
    string Type,
    string? Description,
    string ConfigJson,
    int? EscalationPolicyId = null
);

public record UpdateIntegrationRequest(
    string? Name,
    string? Description,
    string? ConfigJson,
    int? EscalationPolicyId = null
);

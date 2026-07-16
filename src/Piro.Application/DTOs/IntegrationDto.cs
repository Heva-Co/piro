using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

public record IntegrationDto(
    Guid Id,
    string Name,
    IntegrationType Type,
    IntegrationCategory Category,
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
    IntegrationType Type,
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

using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>Outbound representation of a service dependency edge.</summary>
public record DependencyDto(
    string ServiceSlug,
    string DependsOnSlug,
    DependencyPropagationMode PropagationMode,
    DateTime CreatedAt
);

/// <summary>Payload for declaring a new dependency between services.</summary>
public record AddDependencyRequest(
    string DependsOnSlug,
    DependencyPropagationMode PropagationMode
);

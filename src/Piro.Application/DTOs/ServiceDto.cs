using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>Outbound representation of a service, returned by all service endpoints.</summary>
public record ServiceDto(
    int Id,
    string Slug,
    string Name,
    string? Description,
    string? ImageUrl,
    ServiceStatus CurrentStatus,
    ServiceStatus DefaultStatus,
    bool IsHidden,
    int DisplayOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int CheckCount = 0,
    int? EscalationPolicyId = null,
    string? EscalationPolicyName = null
);

/// <summary>Query parameters for the paginated Services list.</summary>
public record ServiceQueryParams(
    int Page = 1,
    int PageSize = 50,
    string? Search = null
);

/// <summary>Payload for creating a new service.</summary>
public record CreateServiceRequest(
    string Slug,
    string Name,
    string? Description,
    string? ImageUrl,
    ServiceStatus DefaultStatus,
    bool IsHidden,
    int DisplayOrder,
    int? EscalationPolicyId = null
);

/// <summary>
/// Payload for updating an existing service — a patch: every field is optional and an omitted field
/// leaves the stored value untouched. <see cref="EscalationPolicyId"/> is tri-state because it is
/// nullable in its own right, so "omitted" and "set to null" must stay distinguishable (RFC 0019 §4.4).
/// </summary>
public record UpdateServiceRequest(
    string? Name,
    string? Description,
    string? ImageUrl,
    ServiceStatus? DefaultStatus,
    bool? IsHidden,
    int? DisplayOrder,
    Patch<int?>? EscalationPolicyId
);

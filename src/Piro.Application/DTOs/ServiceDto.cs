using Piro.Application.Models;
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
    int HistoryDaysDesktop,
    int HistoryDaysMobile,
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
/// Payload for updating an existing service. All fields are optional — omit a property to leave
/// it unchanged. <see cref="EscalationPolicyId"/> uses <see cref="Optional{T}"/> so the client can
/// send it as JSON <c>null</c> to explicitly clear the assignment, distinct from omitting it.
/// </summary>
public record UpdateServiceRequest(
    string? Name,
    string? Description,
    string? ImageUrl,
    ServiceStatus? DefaultStatus,
    bool? IsHidden,
    int? DisplayOrder,
    int? HistoryDaysDesktop,
    int? HistoryDaysMobile,
    Optional<int?> EscalationPolicyId = default
);

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
    int CheckCount = 0
);

/// <summary>Payload for creating a new service.</summary>
public record CreateServiceRequest(
    string Slug,
    string Name,
    string? Description,
    string? ImageUrl,
    ServiceStatus DefaultStatus,
    bool IsHidden,
    int DisplayOrder
);

/// <summary>Payload for updating an existing service. All fields are optional.</summary>
public record UpdateServiceRequest(
    string? Name,
    string? Description,
    string? ImageUrl,
    ServiceStatus? DefaultStatus,
    bool? IsHidden,
    int? DisplayOrder,
    int? HistoryDaysDesktop,
    int? HistoryDaysMobile
);

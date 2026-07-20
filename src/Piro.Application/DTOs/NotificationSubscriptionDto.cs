using System.ComponentModel.DataAnnotations;
using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

public record NotificationSubscriptionDto(
    Guid Id,
    string Name,
    /// <summary>Catalog event wire names this subscription fires on (e.g. "alert:created").</summary>
    IReadOnlyList<string> Events,
    AlertSeverity MinSeverity,
    NotificationTargetKind TargetKind,
    int? UserId,
    string? UserName,
    Guid? IntegrationId,
    string? IntegrationName,
    string? Target,
    bool Enabled
);

public record UpsertNotificationSubscriptionRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [Required, MinLength(1)] IReadOnlyList<string> Events,
    AlertSeverity MinSeverity,
    NotificationTargetKind TargetKind,
    int? UserId,
    Guid? IntegrationId,
    [StringLength(256)] string? Target,
    bool Enabled
);

/// <summary>A page of <see cref="NotificationSubscriptionDto"/> results plus the total matching count.</summary>
public record NotificationSubscriptionPageDto(
    IEnumerable<NotificationSubscriptionDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

/// <summary>
/// A catalog event exposed to the admin UI (RFC 0009) — its stable wire name and description.
/// </summary>
public record NotificationEventCatalogDto(
    string Name,
    string Description
);

/// <summary>
/// One delivery attempt for the admin activity feed 
/// </summary>
public record NotificationDeliveryLogDto(
    long Id,
    string EventType,
    Guid? SubscriptionId,
    string TargetKind,
    IntegrationType? IntegrationType,
    Guid? IntegrationId,
    string TargetDescriptor,
    DeliveryStatus Status,
    string? Error,
    DateTime AttemptedAt
);

/// <summary>
/// A page of <see cref="NotificationDeliveryLogDto"/> results plus the total matching count.
/// </summary>
public record NotificationDeliveryLogPageDto(
    IEnumerable<NotificationDeliveryLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

using Piro.Domain.Enums;

namespace Piro.Application.Models;

/// <summary>Fired when a check execution changes the check's status.</summary>
/// <param name="CheckId">ID of the check that was executed.</param>
/// <param name="ServiceId">ID of the parent service.</param>
/// <param name="PreviousStatus">Status before this execution.</param>
/// <param name="NewStatus">Status after this execution.</param>
public record CheckStatusChangedEvent(
    int CheckId,
    int ServiceId,
    ServiceStatus PreviousStatus,
    ServiceStatus NewStatus
);

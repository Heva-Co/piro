using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>Sends a notification through a specific channel when an alert fires or recovers.</summary>
public interface ITriggerDispatcher
{
    TriggerType Type { get; }
    Task DispatchAsync(Trigger trigger, AlertNotificationContext context, CancellationToken ct = default);
}

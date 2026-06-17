using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>Sends a notification through a specific channel when an alert fires or recovers.</summary>
public interface INotificationChannelDispatcher
{
    NotificationChannelType Type { get; }
    Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default);
}

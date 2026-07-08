using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>Sends alert notifications via a specific integration (to a channel or a personal handle).</summary>
public interface INotificationDispatcher
{
    IntegrationType Type { get; }
    Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default);

    /// <summary>
    /// Sends a notification to a personal handle using the integration's credentials directly.
    /// Returns <c>true</c> if this dispatcher supports personal dispatch; <c>false</c> otherwise
    /// (e.g. webhook-based integrations where the URL encodes the destination).
    /// </summary>
    Task<bool> DispatchPersonalAsync(Integration integration, string handle, AlertNotificationContext context, CancellationToken ct = default);
}

using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>Sends alert notifications to a personal on-call handle via a specific integration.</summary>
public interface INotificationDispatcher
{
    IntegrationType Type { get; }

    /// <summary>
    /// Sends a notification to a personal handle. <paramref name="integration"/> carries shared
    /// platform credentials and is null for channels that don't need one (see
    /// <c>PersonalNotificationChannelExtensions.RequiresIntegration</c> — today, only Email),
    /// where <paramref name="handle"/> alone (an address, chat id, phone number) is self-sufficient.
    /// Returns <c>true</c> if this dispatcher supports personal dispatch; <c>false</c> otherwise.
    /// </summary>
    Task<bool> DispatchPersonalAsync(Integration? integration, string handle, AlertNotificationContext context, CancellationToken ct = default);

    /// <summary>
    /// Sends a plain-text message to a personal handle — used to deliver a one-time verification
    /// code when the user adds/changes a notification preference, independent of the alert
    /// templates used by <see cref="DispatchPersonalAsync"/>. Same null-integration convention.
    /// Returns <c>true</c> if this dispatcher supports it; <c>false</c> otherwise.
    /// </summary>
    Task<bool> SendPersonalMessageAsync(Integration? integration, string handle, string message, CancellationToken ct = default);
}

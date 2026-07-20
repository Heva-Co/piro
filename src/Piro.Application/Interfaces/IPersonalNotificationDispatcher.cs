using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>
/// Delivers a notification to <em>one person's</em> handle (chat id, phone number, email) — delivery
/// mode 1 (RFC 0009 §4.1). Generic over the content type so a dispatcher that cannot carry a given
/// content simply does not implement that instantiation: "Slack cannot send a personal notification"
/// is a type-system fact resolvable at wiring time, not a runtime <c>false</c>.
/// </summary>
/// <typeparam name="TContent">The content this dispatcher renders and sends.</typeparam>
public interface IPersonalNotificationDispatcher<in TContent> where TContent : INotificationContent
{
    IntegrationType Type { get; }

    /// <summary>
    /// Sends <paramref name="content"/> to a personal handle. <paramref name="integration"/> carries
    /// shared platform credentials and is null for channels that don't need one (see
    /// <c>PersonalNotificationChannelExtensions.RequiresIntegration</c> — today, only Email), where
    /// <paramref name="handle"/> alone is self-sufficient. Returns <c>true</c> on success; <c>false</c>
    /// (logged) on a clean failure so a caller can fall through to the next preference or skip.
    /// </summary>
    Task<bool> SendAsync(Integration? integration, string handle, TContent content, CancellationToken ct = default);
}

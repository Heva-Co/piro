using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>
/// Posts a notification to a <em>shared team space</em> — a channel, room, or topic that a whole team
/// watches, not one individual — delivery mode 2 (RFC 0009 §4.1). Generic over the content type for
/// the same reason as <see cref="IPersonalNotificationDispatcher{TContent}"/>.
/// </summary>
/// <typeparam name="TContent">The content this dispatcher renders and posts.</typeparam>
public interface IChannelNotificationDispatcher<in TContent> where TContent : INotificationContent
{
    IntegrationType Type { get; }

    /// <summary>Open string discriminator (RFC 0016 §4.4), defaulted from <see cref="Type"/> during the transition; dispatch moves to it in 5b.</summary>
    string IntegrationId => Type.ToString();

    /// <summary>
    /// Posts <paramref name="content"/> to a group destination. <paramref name="integration"/> is
    /// always required (it holds the channel credentials); <paramref name="target"/> is the
    /// room/space/topic, or null when the integration's config already fully identifies the
    /// destination (e.g. a Slack incoming-webhook URL that is itself channel-specific). Returns
    /// <c>true</c> on success; <c>false</c> (logged) on a clean failure without aborting a fan-out.
    /// </summary>
    Task<bool> SendAsync(Integration integration, string? target, TContent content, CancellationToken ct = default);
}

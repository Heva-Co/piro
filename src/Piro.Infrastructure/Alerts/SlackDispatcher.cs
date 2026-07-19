using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Posts an alert notification to a Slack Incoming Webhook URL.</summary>
public partial class SlackDispatcher(IHttpClientFactory httpClientFactory, ILogger<SlackDispatcher> logger)
    : IChannelNotificationDispatcher<AlertNotificationContext>
{
    public IntegrationType Type => IntegrationType.Slack;

    // Channel delivery is implemented in RFC 0009 phase 5; not registered until then.
    public Task<bool> SendAsync(Integration integration, string? target, AlertNotificationContext content, CancellationToken ct = default) =>
        Task.FromResult(false);
}

using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>POSTs a JSON payload to a webhook URL when an alert fires or recovers.</summary>
public partial class WebhookDispatcher(IHttpClientFactory httpClientFactory, ILogger<WebhookDispatcher> logger)
    : IChannelNotificationDispatcher<AlertNotificationContext>
{
    public IntegrationType Type => IntegrationType.Webhook;

    // Channel delivery is implemented in RFC 0009 phase 5; not registered until then.
    public Task<bool> SendAsync(Integration integration, string? target, AlertNotificationContext content, CancellationToken ct = default) =>
        Task.FromResult(false);
}

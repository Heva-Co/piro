using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Opens and closes Opsgenie alerts via the REST API.</summary>
public class OpsgenieDispatcher(IHttpClientFactory httpClientFactory, ILogger<OpsgenieDispatcher> logger)
    : IChannelNotificationDispatcher<AlertNotificationContext>
{
    public IntegrationType Type => IntegrationType.Opsgenie;

    // RFC 0009 classifies Opsgenie as a mode-3 integration (ISystemEventDispatcher, RFC 0004);
    // until that lands it stays an unregistered stub. Not delivered in phase 1.
    public Task<bool> SendAsync(Integration integration, string? target, AlertNotificationContext content, CancellationToken ct = default) =>
        Task.FromResult(false);
}

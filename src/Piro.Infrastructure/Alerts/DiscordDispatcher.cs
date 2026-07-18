using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Posts an alert notification to a Discord Incoming Webhook.</summary>
public partial class DiscordDispatcher(IHttpClientFactory httpClientFactory, ILogger<DiscordDispatcher> logger)
    : IGroupNotificationDispatcher<AlertNotificationContext>
{
    public IntegrationType Type => IntegrationType.Discord;

    // Group delivery is implemented in RFC 0009 phase 5; not registered until then.
    public Task<bool> SendAsync(Integration integration, string? target, AlertNotificationContext content, CancellationToken ct = default) =>
        Task.FromResult(false);
}

using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Posts an alert notification to a Discord Incoming Webhook.</summary>
public partial class DiscordDispatcher(IHttpClientFactory httpClientFactory, ILogger<DiscordDispatcher> logger)
    : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.Discord;

    public Task<bool> DispatchPersonalAsync(Integration? integration, string handle, AlertNotificationContext context, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<bool> SendPersonalMessageAsync(Integration? integration, string handle, string message, CancellationToken ct = default) =>
        Task.FromResult(false);
}

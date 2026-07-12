using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>POSTs a JSON payload to a webhook URL when an alert fires or recovers.</summary>
public partial class WebhookDispatcher(IHttpClientFactory httpClientFactory, ILogger<WebhookDispatcher> logger)
    : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.Webhook;

    public Task<bool> DispatchPersonalAsync(Integration? integration, string handle, AlertNotificationContext context, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<bool> SendPersonalMessageAsync(Integration? integration, string handle, string message, CancellationToken ct = default) =>
        Task.FromResult(false);
}

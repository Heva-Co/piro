using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Opens and closes Opsgenie alerts via the REST API.</summary>
public class OpsgenieDispatcher(IHttpClientFactory httpClientFactory, ILogger<OpsgenieDispatcher> logger)
    : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.Opsgenie;

    public Task<bool> DispatchPersonalAsync(Integration? integration, string handle, AlertNotificationContext context, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<bool> SendPersonalMessageAsync(Integration? integration, string handle, string message, CancellationToken ct = default) =>
        Task.FromResult(false);
}

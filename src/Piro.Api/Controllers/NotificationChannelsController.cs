using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Api.Controllers;

/// <summary>Manages notification channel configurations.</summary>
[Authorize]
[ApiController]
[Route("api/v1/notification-channels")]
[Produces("application/json")]
public class NotificationChannelsController(
    NotificationChannelAppService channelApp,
    IIntegrationRepository integrationRepository,
    IEnumerable<INotificationChannelDispatcher> dispatchers) : ControllerBase
{
    private readonly Dictionary<IntegrationType, INotificationChannelDispatcher> _dispatchers =
        dispatchers.ToDictionary(d => d.Type);

    /// <summary>Returns all configured notification channels.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<NotificationChannelDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await channelApp.GetAllAsync(ct));

    /// <summary>Returns a single notification channel by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<NotificationChannelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await channelApp.GetByIdAsync(id, ct));

    /// <summary>Creates a new notification channel.</summary>
    [HttpPost]
    [ProducesResponseType<NotificationChannelDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateNotificationChannelRequest request, CancellationToken ct)
    {
        var created = await channelApp.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing notification channel.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<NotificationChannelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateNotificationChannelRequest request, CancellationToken ct) =>
        Ok(await channelApp.UpdateAsync(id, request, ct));

    /// <summary>Deletes a notification channel.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await channelApp.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Sends a test notification using the provided channel configuration.
    /// The channel does not need to be saved first.
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Test([FromBody] TestNotificationChannelRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<IntegrationType>(request.Type, out var channelType))
            return BadRequest(new { error = $"Unknown notification channel type: {request.Type}" });

        if (!_dispatchers.TryGetValue(channelType, out var dispatcher))
            return BadRequest(new { error = $"No dispatcher available for type: {request.Type}" });

        // Merge integration ConfigJson (credentials) with channel MetaJson (target/overrides)
        var mergedMeta = request.MetaJson ?? "{}";
        if (request.IntegrationId is not null)
        {
            var integration = await integrationRepository.GetByIdAsync(request.IntegrationId.Value, ct);
            if (integration is null)
                return BadRequest(new { error = $"Integration {request.IntegrationId} not found." });

            try
            {
                var credDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(integration.ConfigJson ?? "{}") ?? [];
                var targetDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(mergedMeta) ?? [];
                // Target overrides credentials for same keys
                foreach (var kv in targetDict) credDict[kv.Key] = kv.Value;
                mergedMeta = JsonSerializer.Serialize(credDict);
            }
            catch
            {
                // If merge fails fall back to raw configJson
                mergedMeta = integration.ConfigJson ?? "{}";
            }
        }

        var channel = new NotificationChannel
        {
            Id = 0,
            Name = request.Name ?? "Test Channel",
            Type = channelType,
            MetaJson = mergedMeta
        };

        var context = new AlertNotificationContext(
            ServiceName: "Example Service",
            CheckName: "Health Check",
            CurrentStatus: ServiceStatus.DOWN,
            AlertDescription: "This is a test notification from Piro.",
            Severity: AlertSeverity.Warning,
            IsRecovery: false,
            FiredAt: DateTime.UtcNow
        );

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await dispatcher.DispatchAsync(channel, context, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return BadRequest(new { error = "Timeout: the notification could not be delivered within 20 seconds. Check your SMTP/webhook configuration." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        return Ok(new { message = "Test notification sent." });
    }
}

public record TestNotificationChannelRequest(string Type, string? MetaJson = null, string? Name = null, int? IntegrationId = null);

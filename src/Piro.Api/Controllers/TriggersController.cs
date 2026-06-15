using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Api.Controllers;

/// <summary>Manages notification channel (trigger) configurations.</summary>
[Authorize]
[ApiController]
[Route("api/v1/triggers")]
[Produces("application/json")]
public class TriggersController(TriggerAppService triggerApp, IEnumerable<ITriggerDispatcher> dispatchers) : ControllerBase
{
    private readonly Dictionary<TriggerType, ITriggerDispatcher> _dispatchers =
        dispatchers.ToDictionary(d => d.Type);

    /// <summary>Returns all configured triggers.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<TriggerDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await triggerApp.GetAllAsync(ct));

    /// <summary>Returns a single trigger by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<TriggerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await triggerApp.GetByIdAsync(id, ct));

    /// <summary>Creates a new notification trigger.</summary>
    [HttpPost]
    [ProducesResponseType<TriggerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTriggerRequest request, CancellationToken ct)
    {
        var created = await triggerApp.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing trigger.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<TriggerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTriggerRequest request, CancellationToken ct) =>
        Ok(await triggerApp.UpdateAsync(id, request, ct));

    /// <summary>Deletes a trigger.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await triggerApp.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Sends a test notification using the provided trigger configuration.
    /// The trigger does not need to be saved first.
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Test([FromBody] TestTriggerRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<TriggerType>(request.Type, out var triggerType))
            return BadRequest(new { error = $"Unknown trigger type: {request.Type}" });

        if (!_dispatchers.TryGetValue(triggerType, out var dispatcher))
            return BadRequest(new { error = $"No dispatcher available for type: {request.Type}" });

        var trigger = new Trigger
        {
            Id = 0,
            Name = request.Name ?? "Test Trigger",
            Type = triggerType,
            MetaJson = request.MetaJson ?? "{}"
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
            await dispatcher.DispatchAsync(trigger, context, cts.Token);
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

public record TestTriggerRequest(string Type, string? MetaJson = null, string? Name = null);

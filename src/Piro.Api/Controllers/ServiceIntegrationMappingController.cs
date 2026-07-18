using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Infrastructure.Integrations.OAuth;

namespace Piro.Api.Controllers;

/// <summary>
/// Manages the Service↔Integration mappings that route a Piro service's alerts to a discovered remote
/// resource (RFC 0004 §4.5). For PagerDuty, mapping resolves (or provisions) the Events API v2 routing
/// key and stores it per pairing; dispatch later reads the stored key without re-hitting the provider.
/// </summary>
[Authorize(Roles = "Owner,Admin")]
[ApiController]
[Route("api/v1/services/{serviceId:int}/integration-mappings")]
[Produces("application/json")]
public class ServiceIntegrationMappingController(
    IServiceIntegrationMappingRepository mappingRepo,
    IIntegrationRepository integrationRepo,
    IPagerDutyDiscoveryService pagerDutyDiscovery) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Lists the shared-channel integration mappings configured for a service.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ServiceIntegrationMappingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(int serviceId, CancellationToken ct)
    {
        var mappings = await mappingRepo.GetByServiceIdAsync(serviceId, ct);
        return Ok(mappings.Select(ToDto).ToList());
    }

    /// <summary>Maps a service to a discovered remote resource, resolving/provisioning its routing key.</summary>
    [HttpPut]
    [ProducesResponseType<ServiceIntegrationMappingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Upsert(int serviceId, [FromBody] UpsertServiceIntegrationMappingRequest request, CancellationToken ct)
    {
        var integration = await integrationRepo.GetByIdAsync(request.IntegrationId, ct);
        if (integration is null)
            return NotFound(new { title = "Integration not found.", status = 404 });

        try
        {
            // Resolve (or provision) the routing key for the chosen PagerDuty service.
            var routingKey = await pagerDutyDiscovery.ResolveRoutingKeyAsync(request.IntegrationId, request.RemoteId, ct);

            // Look up the service's display name from the live discovery list (best-effort, for UI).
            var discovered = await pagerDutyDiscovery.ListServicesAsync(request.IntegrationId, ct);
            var name = discovered.FirstOrDefault(s => s.Id == request.RemoteId)?.Name;

            var mappingData = new PagerDutyMappingData(request.RemoteId, routingKey, name);
            var mapping = new ServiceIntegrationMapping
            {
                ServiceId = serviceId,
                IntegrationId = request.IntegrationId,
                MappingJson = JsonSerializer.Serialize(mappingData, JsonOptions),
            };
            await mappingRepo.UpsertAsync(mapping, ct);
            return Ok(ToDto(mapping));
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            return BadRequest(new { title = ex.Message, status = 400 });
        }
    }

    /// <summary>Removes a service's mapping to an integration.</summary>
    [HttpDelete("{integrationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int serviceId, Guid integrationId, CancellationToken ct)
    {
        await mappingRepo.DeleteAsync(serviceId, integrationId, ct);
        return NoContent();
    }

    private static ServiceIntegrationMappingDto ToDto(ServiceIntegrationMapping m)
    {
        string? remoteId = null, remoteLabel = null;
        try
        {
            var data = JsonSerializer.Deserialize<PagerDutyMappingData>(m.MappingJson, JsonOptions);
            remoteId = data?.PagerDutyServiceId;
            remoteLabel = data?.PagerDutyServiceName;
        }
        catch (JsonException) { /* unknown mapping shape — leave remote fields null */ }

        return new ServiceIntegrationMappingDto(m.ServiceId, m.IntegrationId, remoteId, remoteLabel);
    }
}

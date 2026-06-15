using System.Threading.Channels;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>Manages service dependency edges and enforces DAG acyclicity.</summary>
/// <remarks>
/// Cycle detection runs synchronously before any edge is persisted.
/// The algorithm performs a BFS from the candidate upstream service;
/// if it can reach the dependent service, the edge would create a cycle.
/// </remarks>
public class DependencyService(
    IServiceRepository serviceRepo,
    IServiceDependencyRepository dependencyRepo,
    Channel<CheckStatusChangedEvent> statusChannel)
{
    public async Task<IEnumerable<DependencyDto>> GetByServiceSlugAsync(string serviceSlug, CancellationToken ct = default)
    {
        var service = await serviceRepo.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);

        var deps = await dependencyRepo.GetByServiceIdAsync(service.Id, ct);
        return deps.Select(d => ToDto(d, serviceSlug));
    }

    public async Task<DependencyDto> AddAsync(string serviceSlug, AddDependencyRequest request, CancellationToken ct = default)
    {
        var service = await serviceRepo.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);

        var upstream = await serviceRepo.GetBySlugAsync(request.DependsOnSlug, ct)
            ?? throw new NotFoundException(nameof(Service), request.DependsOnSlug);

        if (service.Id == upstream.Id)
            throw new DomainValidationException("A service cannot depend on itself.");

        if (await dependencyRepo.ExistsAsync(service.Id, upstream.Id, ct))
            throw new DomainValidationException($"'{serviceSlug}' already depends on '{request.DependsOnSlug}'.");

        await ValidateAcyclicAsync(service.Id, upstream.Id, ct);

        var edge = new ServiceDependency
        {
            ServiceId = service.Id,
            DependsOnServiceId = upstream.Id,
            PropagationMode = request.PropagationMode,
            CreatedAt = DateTime.UtcNow
        };

        var created = await dependencyRepo.CreateAsync(edge, ct);
        created.Service = service;
        created.DependsOnService = upstream;

        // Trigger recomputation so the new dependency is reflected immediately
        TriggerRecompute(service.Id);

        return ToDto(created, serviceSlug);
    }

    public async Task RemoveAsync(string serviceSlug, string dependsOnSlug, CancellationToken ct = default)
    {
        var service = await serviceRepo.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);

        var upstream = await serviceRepo.GetBySlugAsync(dependsOnSlug, ct)
            ?? throw new NotFoundException(nameof(Service), dependsOnSlug);

        var edge = await dependencyRepo.GetAsync(service.Id, upstream.Id, ct)
            ?? throw new NotFoundException("Dependency", $"{serviceSlug} → {dependsOnSlug}");

        await dependencyRepo.DeleteAsync(edge, ct);
        TriggerRecompute(service.Id);
    }

    /// <summary>
    /// BFS from <paramref name="candidateUpstreamId"/> through the existing dependency graph.
    /// If <paramref name="serviceId"/> is reachable, adding this edge would create a cycle.
    /// </summary>
    private async Task ValidateAcyclicAsync(int serviceId, int candidateUpstreamId, CancellationToken ct)
    {
        // BFS from candidateUpstream through the existing graph.
        // If we can reach serviceId, adding this edge would create a cycle.
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(candidateUpstreamId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!visited.Add(current)) continue;

            // Reached the dependent service — this edge would close a cycle
            if (current == serviceId)
                throw new CyclicDependencyException(serviceId.ToString(), candidateUpstreamId.ToString());

            var children = (await dependencyRepo.GetDependsOnIdsAsync(current, ct: ct)).ToList();
            foreach (var child in children.Where(c => !visited.Contains(c)))
                queue.Enqueue(child);
        }
    }

    private void TriggerRecompute(int serviceId) =>
        statusChannel.Writer.TryWrite(new CheckStatusChangedEvent(0, serviceId, ServiceStatus.NO_DATA, ServiceStatus.NO_DATA));

    private static DependencyDto ToDto(ServiceDependency d, string serviceSlug) => new(
        serviceSlug,
        d.DependsOnService?.Slug ?? d.DependsOnServiceId.ToString(),
        d.PropagationMode,
        d.CreatedAt
    );
}

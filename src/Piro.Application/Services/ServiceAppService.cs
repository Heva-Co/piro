using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>Application service for service CRUD operations.</summary>
/// <remarks>
/// Does not compute status — that is handled by <see cref="ServiceStatusService"/>.
/// Slug immutability is enforced: slugs cannot be changed after creation.
/// </remarks>
public class ServiceAppService(
    IServiceRepository repository,
    IEscalationPolicyRepository escalationPolicyRepository,
    ICheckRepository checkRepository,
    ICheckSchedulerService scheduler)
{
    public async Task<PaginatedResponse<ServiceDto>> GetPagedAsync(ServiceQueryParams query, CancellationToken ct = default)
    {
        var (services, total) = await repository.GetPagedAsync(query, ct);
        var counts = await repository.GetCheckCountsAsync(ct);
        var items = services.Select(s => s.ToDto(counts.GetValueOrDefault(s.Id, 0)));
        return new PaginatedResponse<ServiceDto>(items, total, Math.Max(1, query.Page), Math.Clamp(query.PageSize, 10, 200));
    }

    public async Task<ServiceDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var service = await repository.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException(nameof(Service), slug);
        var checkCount = await repository.GetCheckCountAsync(service.Id, ct);
        return service.ToDto(checkCount);
    }

    public async Task<ServiceDto> CreateAsync(CreateServiceRequest request, CancellationToken ct = default)
    {
        if (await repository.SlugExistsAsync(request.Slug, ct))
            throw new DomainValidationException($"A service with slug '{request.Slug}' already exists.");

        if (request.EscalationPolicyId is int policyId)
            _ = await escalationPolicyRepository.GetByIdAsync(policyId, ct)
                ?? throw new NotFoundException(nameof(EscalationPolicy), policyId.ToString());

        var service = new Service
        {
            Slug = request.Slug,
            Name = request.Name,
            Description = request.Description,
            DefaultStatus = request.DefaultStatus,
            CurrentStatus = request.DefaultStatus,
            IsHidden = request.IsHidden,
            DisplayOrder = request.DisplayOrder,
            EscalationPolicyId = request.EscalationPolicyId
        };

        var created = await repository.CreateAsync(service, ct);
        return created.ToDto();
    }

    public async Task<ServiceDto> UpdateAsync(string slug, UpdateServiceRequest request, CancellationToken ct = default)
    {
        var service = await repository.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException(nameof(Service), slug);

        if (request.Name is not null) service.Name = request.Name;
        if (request.Description is not null) service.Description = request.Description;
        if (request.DefaultStatus is not null) service.DefaultStatus = request.DefaultStatus.Value;
        if (request.IsHidden is not null) service.IsHidden = request.IsHidden.Value;
        if (request.DisplayOrder is not null) service.DisplayOrder = request.DisplayOrder.Value;

        // Tri-state: an omitted EscalationPolicyId leaves the field untouched, an explicit null clears
        // it. Previously omission nulled the field, so any partial update silently detached the policy
        // and disabled on-call notifications for the service (RFC 0019 §4.4).
        if (request.EscalationPolicyId is { Value: var requested })
        {
            if (requested is int policyId)
            {
                _ = await escalationPolicyRepository.GetByIdAsync(policyId, ct)
                    ?? throw new NotFoundException(nameof(EscalationPolicy), policyId.ToString());
                service.EscalationPolicyId = policyId;
            }
            else
            {
                service.EscalationPolicyId = null;
            }
        }

        var updated = await repository.UpdateAsync(service, ct);
        return updated.ToDto();
    }

    public async Task DeleteAsync(string slug, CancellationToken ct = default)
    {
        var service = await repository.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException(nameof(Service), slug);

        // Checks cascade at the database level, but their Quartz jobs do not — left scheduled they
        // keep firing against a deleted check id until the process restarts (RFC 0019 §4.5).
        var checks = await checkRepository.GetByServiceIdAsync(service.Id, ct);

        await repository.DeleteAsync(service, ct);

        foreach (var check in checks)
            await scheduler.UnscheduleAsync(check.Id, ct);
    }
}

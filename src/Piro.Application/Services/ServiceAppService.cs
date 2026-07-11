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
public class ServiceAppService(IServiceRepository repository)
{
    public async Task<IEnumerable<ServiceDto>> GetAllAsync(CancellationToken ct = default)
    {
        var services = await repository.GetAllAsync(ct);
        var counts = await repository.GetCheckCountsAsync(ct);
        return services.Select(s => s.ToDto(counts.GetValueOrDefault(s.Id, 0)));
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

        var service = new Service
        {
            Slug = request.Slug,
            Name = request.Name,
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            DefaultStatus = request.DefaultStatus,
            CurrentStatus = request.DefaultStatus,
            IsHidden = request.IsHidden,
            DisplayOrder = request.DisplayOrder
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
        if (request.ImageUrl is not null) service.ImageUrl = request.ImageUrl;
        if (request.DefaultStatus is not null) service.DefaultStatus = request.DefaultStatus.Value;
        if (request.IsHidden is not null) service.IsHidden = request.IsHidden.Value;
        if (request.DisplayOrder is not null) service.DisplayOrder = request.DisplayOrder.Value;
        if (request.HistoryDaysDesktop is not null) service.HistoryDaysDesktop = request.HistoryDaysDesktop.Value;
        if (request.HistoryDaysMobile is not null) service.HistoryDaysMobile = request.HistoryDaysMobile.Value;

        var updated = await repository.UpdateAsync(service, ct);
        return updated.ToDto();
    }

    public async Task DeleteAsync(string slug, CancellationToken ct = default)
    {
        var service = await repository.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException(nameof(Service), slug);
        await repository.DeleteAsync(service, ct);
    }
}

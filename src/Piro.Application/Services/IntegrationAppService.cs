using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;
using Piro.Domain.Extensions;

namespace Piro.Application.Services;

public class IntegrationAppService(IIntegrationRepository repository)
{
    public async Task<IEnumerable<IntegrationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await repository.GetAllAsync(ct);
        return items.Select(ToDto);
    }

    public async Task<IntegrationDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var item = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Integration), id.ToString());
        return ToDto(item);
    }

    public async Task<IntegrationDto> CreateAsync(CreateIntegrationRequest request, CancellationToken ct = default)
    {
        var integration = new Integration
        {
            Name = request.Name,
            Type = request.Type,
            Description = request.Description,
            ConfigJson = request.ConfigJson
        };
        var created = await repository.CreateAsync(integration, ct);
        return ToDto(created);
    }

    public async Task<IntegrationDto> UpdateAsync(int id, UpdateIntegrationRequest request, CancellationToken ct = default)
    {
        var integration = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Integration), id.ToString());

        if (request.Name is not null) integration.Name = request.Name;
        if (request.Description is not null) integration.Description = request.Description;
        if (request.ConfigJson is not null) integration.ConfigJson = request.ConfigJson;

        var updated = await repository.UpdateAsync(integration, ct);
        return ToDto(updated);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var integration = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Integration), id.ToString());

        if (integration.Checks.Count > 0)
            throw new DomainValidationException(
                $"Integration '{integration.Name}' is referenced by {integration.Checks.Count} check(s). Remove or reassign those checks before deleting.");

        await repository.DeleteAsync(integration, ct);
    }

    private static IntegrationDto ToDto(Integration i) => new(
        i.Id, 
        i.Name, 
        i.Type, 
        i.Type.GetCategory(), 
        i.Description, 
        i.ConfigJson,
        i.Checks.Count, 
        i.CreatedAt, 
        i.UpdatedAt
    );
}

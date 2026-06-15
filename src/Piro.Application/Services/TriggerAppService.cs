using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>Application service for notification channel (Trigger) CRUD.</summary>
public class TriggerAppService(
    ITriggerRepository triggerRepository,
    IAlertConfigRepository alertConfigRepository)
{
    public async Task<IEnumerable<TriggerDto>> GetAllAsync(CancellationToken ct = default) =>
        (await triggerRepository.GetAllAsync(ct)).Select(ToDto);

    public async Task<TriggerDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var trigger = await triggerRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Trigger), id.ToString());
        return ToDto(trigger);
    }

    public async Task<TriggerDto> CreateAsync(CreateTriggerRequest request, CancellationToken ct = default)
    {
        var trigger = new Trigger
        {
            Name = request.Name,
            Type = request.Type,
            Description = request.Description,
            MetaJson = request.MetaJson,
            IsGlobal = request.IsGlobal,
            IsLocked = request.IsLocked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var created = await triggerRepository.CreateAsync(trigger, ct);

        if (created.IsGlobal)
            await PropagateToAllConfigsAsync(created, ct);

        return ToDto(created);
    }

    public async Task<TriggerDto> UpdateAsync(int id, UpdateTriggerRequest request, CancellationToken ct = default)
    {
        var trigger = await triggerRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Trigger), id.ToString());

        var wasGlobal = trigger.IsGlobal;

        if (request.Name is not null) trigger.Name = request.Name;
        if (request.Description is not null) trigger.Description = request.Description;
        if (request.Status is not null) trigger.Status = request.Status;
        if (request.MetaJson is not null) trigger.MetaJson = request.MetaJson;
        if (request.IsGlobal is not null) trigger.IsGlobal = request.IsGlobal.Value;
        if (request.IsLocked is not null) trigger.IsLocked = request.IsLocked.Value;
        trigger.UpdatedAt = DateTime.UtcNow;

        var updated = await triggerRepository.UpdateAsync(trigger, ct);

        // Newly marked as global → propagate to all existing AlertConfigs
        if (!wasGlobal && updated.IsGlobal)
            await PropagateToAllConfigsAsync(updated, ct);

        return ToDto(updated);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var trigger = await triggerRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Trigger), id.ToString());
        await triggerRepository.DeleteAsync(trigger, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task PropagateToAllConfigsAsync(Trigger trigger, CancellationToken ct)
    {
        var allConfigs = await alertConfigRepository.GetAllAsync(ct);
        foreach (var config in allConfigs)
        {
            if (config.AlertConfigTriggers.Any(act => act.TriggerId == trigger.Id))
                continue;

            config.AlertConfigTriggers.Add(new AlertConfigTrigger
            {
                AlertConfigId = config.Id,
                TriggerId = trigger.Id,
            });
            await alertConfigRepository.UpdateAsync(config, ct);
        }
    }

    private static TriggerDto ToDto(Trigger t) => new(
        t.Id, t.Name, t.Type, t.Description, t.Status, t.MetaJson,
        t.IsGlobal, t.IsLocked, t.CreatedAt, t.UpdatedAt,
        t.AlertConfigTriggers.Count
    );
}

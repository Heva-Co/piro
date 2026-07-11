using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

public class OnCallScheduleAppService(
    IOnCallScheduleRepository scheduleRepo,
    OnCallService onCallService)
{
    public async Task<List<OnCallScheduleDto>> GetAllAsync(CancellationToken ct = default)
    {
        var schedules = await scheduleRepo.GetAllAsync(ct);
        return schedules.Select(s => s.ToDto()).ToList();
    }

    public Task<List<OnCallScheduleMembersDto>> GetAllWithMembersAsync(CancellationToken ct = default) =>
        scheduleRepo.GetAllWithMembersAsync(ct);

    public async Task<OnCallScheduleDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(id, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), id.ToString());
        return schedule.ToDto();
    }

    public async Task<OnCallScheduleDto> CreateAsync(CreateOnCallScheduleRequest request, CancellationToken ct = default)
    {
        var schedule = new OnCallSchedule
        {
            Name = request.Name,
            Description = request.Description,
            TimeZone = request.TimeZone ?? "UTC",
            NotifyOnShiftStart = request.NotifyOnShiftStart,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
        };
        var created = await scheduleRepo.CreateAsync(schedule, ct);
        return created.ToDto();
    }

    public async Task<OnCallScheduleDto> UpdateAsync(int id, UpdateOnCallScheduleRequest request, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(id, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), id.ToString());

        if (request.Name is not null) schedule.Name = request.Name;
        if (request.Description is not null) schedule.Description = request.Description;
        if (request.TimeZone is not null) schedule.TimeZone = request.TimeZone;
        if (request.NotifyOnShiftStart.HasValue) schedule.NotifyOnShiftStart = request.NotifyOnShiftStart.Value;
        if (request.StartsAtUtc.HasValue) schedule.StartsAtUtc = request.StartsAtUtc;
        if (request.EndsAtUtc.HasValue) schedule.EndsAtUtc = request.EndsAtUtc;

        await scheduleRepo.UpdateAsync(schedule, ct);
        return schedule.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var exists = await scheduleRepo.GetByIdWithLayersAsync(id, ct);
        if (exists is null) throw new NotFoundException(nameof(OnCallSchedule), id.ToString());
        await scheduleRepo.DeleteAsync(id, ct);
    }

    public Task<IReadOnlyList<AppUser>> GetCurrentOnCallUsersAsync(int scheduleId, CancellationToken ct = default)
        => onCallService.GetCurrentOnCallUsersAsync(scheduleId, ct);

    public Task<List<OnCallSlotDto>> ExpandAsync(int scheduleId, DateTimeOffset from, DateTimeOffset to, bool applyOverrides = true, CancellationToken ct = default)
        => onCallService.ExpandScheduleAsync(scheduleId, from, to, applyOverrides, ct);

    public async Task<OnCallLayerDto> CreateLayerAsync(int scheduleId, CreateOnCallLayerRequest request, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), scheduleId.ToString());

        ValidateLayerRequest(request.FirstOccurrenceStartsAt, request.FirstOccurrenceEndsAt, request.UserIds);

        var order = schedule.Layers.Count > 0 ? schedule.Layers.Max(l => l.Order) + 1 : 0;

        var layer = new OnCallLayer
        {
            ScheduleId = scheduleId,
            Name = request.Name,
            Order = order,
            RecurrenceRule = request.RecurrenceRule,
            FirstOccurrenceStartsAt = request.FirstOccurrenceStartsAt,
            FirstOccurrenceEndsAt = request.FirstOccurrenceEndsAt,
            Users = request.UserIds.Select((uid, idx) => new OnCallLayerUser
            {
                UserId = uid,
                Position = idx,
            }).ToList(),
        };

        var created = await scheduleRepo.CreateLayerAsync(layer, ct);
        return created.ToDto();
    }

    public async Task<OnCallLayerDto> UpdateLayerAsync(int scheduleId, int layerId, UpdateOnCallLayerRequest request, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), scheduleId.ToString());
        var layer = schedule.Layers.FirstOrDefault(l => l.Id == layerId)
            ?? throw new NotFoundException(nameof(OnCallLayer), layerId.ToString());

        ValidateLayerRequest(request.FirstOccurrenceStartsAt, request.FirstOccurrenceEndsAt, request.UserIds);

        layer.Name = request.Name;
        layer.RecurrenceRule = request.RecurrenceRule;
        layer.FirstOccurrenceStartsAt = request.FirstOccurrenceStartsAt;
        layer.FirstOccurrenceEndsAt = request.FirstOccurrenceEndsAt;
        layer.Users = request.UserIds.Select((uid, idx) => new OnCallLayerUser
        {
            LayerId = layerId,
            UserId = uid,
            Position = idx,
        }).ToList();

        var updated = await scheduleRepo.UpdateLayerAsync(layer, ct);
        return updated.ToDto();
    }

    public async Task DeleteLayerAsync(int scheduleId, int layerId, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), scheduleId.ToString());
        if (schedule.Layers.All(l => l.Id != layerId))
            throw new NotFoundException(nameof(OnCallLayer), layerId.ToString());
        await scheduleRepo.DeleteLayerAsync(layerId, ct);
    }

    public async Task<OnCallOverrideDto> CreateOverrideAsync(int scheduleId, CreateOnCallOverrideRequest request, CancellationToken ct = default)
    {
        _ = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), scheduleId.ToString());

        var ov = new OnCallOverride
        {
            ScheduleId = scheduleId,
            UserId = request.UserId,
            ReplacesUserId = request.ReplacesUserId,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
            Reason = request.Reason,
        };
        var saved = await scheduleRepo.CreateOverrideAsync(ov, ct);
        return saved.ToDto();
    }

    public async Task DeleteOverrideAsync(int scheduleId, int overrideId, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), scheduleId.ToString());
        if (schedule.Overrides.All(o => o.Id != overrideId))
            throw new NotFoundException(nameof(OnCallOverride), overrideId.ToString());
        await scheduleRepo.DeleteOverrideAsync(overrideId, ct);
    }

    private static void ValidateLayerRequest(DateTimeOffset startsAt, DateTimeOffset endsAt, List<int> userIds)
    {
        if (endsAt <= startsAt)
            throw new DomainValidationException("Layer's first occurrence end must be after its start.");
        if (userIds.Count == 0)
            throw new DomainValidationException("A rotation layer must have at least one user.");
    }
}

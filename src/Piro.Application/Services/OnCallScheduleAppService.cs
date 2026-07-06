using Piro.Application.DTOs;
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
        return schedules.Select(ToDto).ToList();
    }

    public async Task<OnCallScheduleDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(id, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), id.ToString());
        return ToDto(schedule);
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
        return ToDto(created);
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
        return ToDto(schedule);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var exists = await scheduleRepo.GetByIdWithLayersAsync(id, ct);
        if (exists is null) throw new NotFoundException(nameof(OnCallSchedule), id.ToString());
        await scheduleRepo.DeleteAsync(id, ct);
    }

    public Task<IReadOnlyList<AppUser>> GetCurrentOnCallUsersAsync(int scheduleId, CancellationToken ct = default)
        => onCallService.GetCurrentOnCallUsersAsync(scheduleId, ct);

    public Task<List<OnCallSlotDto>> ExpandAsync(int scheduleId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        => onCallService.ExpandScheduleAsync(scheduleId, from, to, ct);

    public async Task<OnCallLayerDto> CreateLayerAsync(int scheduleId, CreateOnCallLayerRequest request, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), scheduleId.ToString());

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
        return LayerToDto(created);
    }

    public async Task<OnCallLayerDto> UpdateLayerAsync(int scheduleId, int layerId, UpdateOnCallLayerRequest request, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), scheduleId.ToString());
        var layer = schedule.Layers.FirstOrDefault(l => l.Id == layerId)
            ?? throw new NotFoundException(nameof(OnCallLayer), layerId.ToString());

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
        return LayerToDto(updated);
    }

    public async Task DeleteLayerAsync(int scheduleId, int layerId, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), scheduleId.ToString());
        if (schedule.Layers.All(l => l.Id != layerId))
            throw new NotFoundException(nameof(OnCallLayer), layerId.ToString());
        await scheduleRepo.DeleteLayerAsync(layerId, ct);
    }

    private static OnCallScheduleDto ToDto(OnCallSchedule s) => new(
        s.Id, s.Name, s.Description, s.TimeZone, s.NotifyOnShiftStart,
        s.StartsAtUtc, s.EndsAtUtc, s.CreatedAt, s.UpdatedAt,
        s.Layers.OrderBy(l => l.Order).Select(LayerToDto).ToList());

    private static bool IsAllDay(DateTimeOffset start, DateTimeOffset end)
    {
        var s = start.ToUniversalTime();
        var e = end.ToUniversalTime();
        return s.TimeOfDay == TimeSpan.Zero
            && (e.TimeOfDay == new TimeSpan(23, 59, 59) || e.TimeOfDay == new TimeSpan(23, 59, 0));
    }

    private static OnCallLayerDto LayerToDto(OnCallLayer l) => new(
        l.Id, l.ScheduleId, l.Name, l.Order, l.RecurrenceRule,
        l.FirstOccurrenceStartsAt, l.FirstOccurrenceEndsAt,
        IsAllDay(l.FirstOccurrenceStartsAt, l.FirstOccurrenceEndsAt),
        l.Users.OrderBy(u => u.Position).Select(u => new OnCallLayerUserDto(
            u.Id, u.UserId, u.User?.Name ?? string.Empty,
            GetInitials(u.User?.Name ?? string.Empty),
            u.User?.Color ?? "#6366f1",
            u.Position)).ToList());

    private static string GetInitials(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
        };
    }
}

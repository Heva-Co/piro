using Microsoft.AspNetCore.Identity;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

public class OnCallScheduleAppService(
    IOnCallScheduleRepository scheduleRepo,
    OnCallService onCallService,
    IUnitOfWork uow,
    UserManager<AppUser> userManager)
{
    public async Task<OnCallSchedulePageDto> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await scheduleRepo.GetPagedAsync(page, pageSize, ct);
        return new OnCallSchedulePageDto(
            items.Select(s => s.ToDto()),
            total,
            Math.Max(1, page),
            Math.Clamp(pageSize, 10, 200));
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

    /// <summary>The calling user's own on-call slots across every schedule they appear in — for their personal calendar.</summary>
    public Task<List<OnCallSlotDto>> GetMySlotsAsync(int userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        => onCallService.GetMySlotsAsync(userId, from, to, ct);

    /// <summary>Whether the calling user is on-call right now, and for which schedule — for the "you're on-call" indicator.</summary>
    public Task<OnCallSlotDto?> GetMyCurrentStatusAsync(int userId, CancellationToken ct = default)
        => onCallService.GetMyCurrentStatusAsync(userId, ct);

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

    /// <summary>
    /// Resolves a draft batch (not yet saved) into slots and coverage gaps over the given range,
    /// so the "Save" button can warn the user about uncovered windows before committing. Builds
    /// the schedule entirely in memory — nothing is persisted.
    /// </summary>
    public async Task<RotationsPreviewDto> PreviewRotationsAsync(
        int scheduleId, SaveRotationsRequest request, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), scheduleId.ToString());

        var deleteLayerIds = request.LayerIdsToDelete.ToHashSet();
        var updateById = request.LayersToUpdate.ToDictionary(r => r.LayerId);

        var draftLayers = new List<OnCallLayer>();
        foreach (var layer in schedule.Layers)
        {
            if (deleteLayerIds.Contains(layer.Id)) continue;
            if (updateById.TryGetValue(layer.Id, out var upd))
            {
                draftLayers.Add(new OnCallLayer
                {
                    Id = layer.Id,
                    ScheduleId = scheduleId,
                    Name = upd.Name,
                    Order = layer.Order,
                    RecurrenceRule = upd.RecurrenceRule,
                    FirstOccurrenceStartsAt = upd.FirstOccurrenceStartsAt,
                    FirstOccurrenceEndsAt = upd.FirstOccurrenceEndsAt,
                    Users = await ResolveLayerUsersAsync(upd.UserIds, ct),
                });
            }
            else
            {
                draftLayers.Add(layer);
            }
        }

        var nextOrder = draftLayers.Count > 0 ? draftLayers.Max(l => l.Order) + 1 : 0;
        foreach (var req in request.LayersToCreate)
        {
            draftLayers.Add(new OnCallLayer
            {
                Id = 0,
                ScheduleId = scheduleId,
                Name = req.Name,
                Order = nextOrder++,
                RecurrenceRule = req.RecurrenceRule,
                FirstOccurrenceStartsAt = req.FirstOccurrenceStartsAt,
                FirstOccurrenceEndsAt = req.FirstOccurrenceEndsAt,
                Users = await ResolveLayerUsersAsync(req.UserIds, ct),
            });
        }

        var deleteOverrideIds = request.OverrideIdsToDelete.ToHashSet();
        var draftOverrides = schedule.Overrides.Where(o => !deleteOverrideIds.Contains(o.Id)).ToList();
        foreach (var req in request.OverridesToCreate)
        {
            var user = await userManager.FindByIdAsync(req.UserId.ToString())
                ?? throw new NotFoundException(nameof(AppUser), req.UserId.ToString());
            AppUser? replacesUser = req.ReplacesUserId.HasValue
                ? await userManager.FindByIdAsync(req.ReplacesUserId.Value.ToString())
                : null;

            draftOverrides.Add(new OnCallOverride
            {
                Id = 0,
                ScheduleId = scheduleId,
                UserId = req.UserId,
                User = user,
                ReplacesUserId = req.ReplacesUserId,
                ReplacesUser = replacesUser,
                StartsAtUtc = req.StartsAtUtc,
                EndsAtUtc = req.EndsAtUtc,
                Reason = req.Reason,
            });
        }

        var draftSchedule = new OnCallSchedule
        {
            Id = schedule.Id,
            Name = schedule.Name,
            TimeZone = schedule.TimeZone,
            StartsAtUtc = schedule.StartsAtUtc,
            EndsAtUtc = schedule.EndsAtUtc,
            Layers = draftLayers,
            Overrides = draftOverrides,
        };

        var slots = onCallService.ExpandSchedule(draftSchedule, from, to, applyOverrides: true);
        var gaps = FindCoverageGaps(slots, from, to);

        return new RotationsPreviewDto(slots, gaps);
    }

    private async Task<List<OnCallLayerUser>> ResolveLayerUsersAsync(List<int> userIds, CancellationToken ct)
    {
        var result = new List<OnCallLayerUser>();
        for (var i = 0; i < userIds.Count; i++)
        {
            var user = await userManager.FindByIdAsync(userIds[i].ToString())
                ?? throw new NotFoundException(nameof(AppUser), userIds[i].ToString());
            result.Add(new OnCallLayerUser { UserId = userIds[i], User = user, Position = i });
        }
        return result;
    }

    /// <summary>
    /// Gaps shorter than this are treated as scheduling-precision noise (e.g. a layer configured
    /// with a 23:59:59 shift instead of exactly 24h leaves a 1-second gap every day) rather than
    /// real uncovered windows worth warning about.
    /// </summary>
    private static readonly TimeSpan MinReportableGap = TimeSpan.FromMinutes(1);

    /// <summary>Finds windows in [from, to) not covered by any slot — merges overlapping/adjacent slots first.</summary>
    private static List<CoverageGapDto> FindCoverageGaps(List<OnCallSlotDto> slots, DateTimeOffset from, DateTimeOffset to)
    {
        var gaps = new List<CoverageGapDto>();
        if (from >= to) return gaps;

        var intervals = slots
            .Select(s => (Start: s.StartsAt < from ? from : s.StartsAt, End: s.EndsAt > to ? to : s.EndsAt))
            .Where(i => i.Start < i.End)
            .OrderBy(i => i.Start)
            .ToList();

        var cursor = from;
        foreach (var (start, end) in intervals)
        {
            if (start - cursor >= MinReportableGap)
                gaps.Add(new CoverageGapDto(cursor, start));
            if (end > cursor)
                cursor = end;
        }
        if (to - cursor >= MinReportableGap)
            gaps.Add(new CoverageGapDto(cursor, to));

        return gaps;
    }

    /// <summary>
    /// Applies a batch of layer/override create/update/delete operations to one schedule
    /// atomically — all succeed or the whole batch rolls back, so the "Save" button on the
    /// schedule detail page never leaves rotations half-applied.
    /// </summary>
    public async Task<OnCallScheduleDto> SaveRotationsAsync(int scheduleId, SaveRotationsRequest request, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), scheduleId.ToString());

        foreach (var req in request.LayersToUpdate)
            if (schedule.Layers.All(l => l.Id != req.LayerId))
                throw new NotFoundException(nameof(OnCallLayer), req.LayerId.ToString());
        foreach (var layerId in request.LayerIdsToDelete)
            if (schedule.Layers.All(l => l.Id != layerId))
                throw new NotFoundException(nameof(OnCallLayer), layerId.ToString());
        foreach (var overrideId in request.OverrideIdsToDelete)
            if (schedule.Overrides.All(o => o.Id != overrideId))
                throw new NotFoundException(nameof(OnCallOverride), overrideId.ToString());

        foreach (var req in request.LayersToCreate)
            ValidateLayerRequest(req.FirstOccurrenceStartsAt, req.FirstOccurrenceEndsAt, req.UserIds);
        foreach (var req in request.LayersToUpdate)
            ValidateLayerRequest(req.FirstOccurrenceStartsAt, req.FirstOccurrenceEndsAt, req.UserIds);

        await uow.BeginAsync(ct);
        try
        {
            foreach (var layerId in request.LayerIdsToDelete)
                await scheduleRepo.DeleteLayerAsync(layerId, ct);

            foreach (var overrideId in request.OverrideIdsToDelete)
                await scheduleRepo.DeleteOverrideAsync(overrideId, ct);

            foreach (var req in request.LayersToUpdate)
            {
                var layer = schedule.Layers.First(l => l.Id == req.LayerId);
                layer.Name = req.Name;
                layer.RecurrenceRule = req.RecurrenceRule;
                layer.FirstOccurrenceStartsAt = req.FirstOccurrenceStartsAt;
                layer.FirstOccurrenceEndsAt = req.FirstOccurrenceEndsAt;
                layer.Users = req.UserIds.Select((uid, idx) => new OnCallLayerUser
                {
                    LayerId = req.LayerId,
                    UserId = uid,
                    Position = idx,
                }).ToList();
                await scheduleRepo.UpdateLayerAsync(layer, ct);
            }

            var nextOrder = schedule.Layers.Count > 0 ? schedule.Layers.Max(l => l.Order) + 1 : 0;
            foreach (var req in request.LayersToCreate)
            {
                var layer = new OnCallLayer
                {
                    ScheduleId = scheduleId,
                    Name = req.Name,
                    Order = nextOrder++,
                    RecurrenceRule = req.RecurrenceRule,
                    FirstOccurrenceStartsAt = req.FirstOccurrenceStartsAt,
                    FirstOccurrenceEndsAt = req.FirstOccurrenceEndsAt,
                    Users = req.UserIds.Select((uid, idx) => new OnCallLayerUser
                    {
                        UserId = uid,
                        Position = idx,
                    }).ToList(),
                };
                await scheduleRepo.CreateLayerAsync(layer, ct);
            }

            foreach (var req in request.OverridesToCreate)
            {
                var ov = new OnCallOverride
                {
                    ScheduleId = scheduleId,
                    UserId = req.UserId,
                    ReplacesUserId = req.ReplacesUserId,
                    StartsAtUtc = req.StartsAtUtc,
                    EndsAtUtc = req.EndsAtUtc,
                    Reason = req.Reason,
                };
                await scheduleRepo.CreateOverrideAsync(ov, ct);
            }

            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }

        var updated = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct)
            ?? throw new NotFoundException(nameof(OnCallSchedule), scheduleId.ToString());
        return updated.ToDto();
    }

    private static void ValidateLayerRequest(DateTimeOffset startsAt, DateTimeOffset endsAt, List<int> userIds)
    {
        if (endsAt <= startsAt)
            throw new DomainValidationException("Layer's first occurrence end must be after its start.");
        if (userIds.Count == 0)
            throw new DomainValidationException("A rotation layer must have at least one user.");
    }
}

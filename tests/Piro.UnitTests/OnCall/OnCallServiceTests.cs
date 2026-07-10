using FluentAssertions;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Infrastructure;

namespace Piro.UnitTests.OnCall;

public class OnCallServiceTests
{
    private class FakeScheduleRepository(OnCallSchedule schedule) : IOnCallScheduleRepository
    {
        public Task<List<OnCallSchedule>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<OnCallSchedule> { schedule });
        public Task<List<OnCallScheduleMembersDto>> GetAllWithMembersAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<OnCallScheduleMembersDto>());
        public Task<OnCallSchedule?> GetByIdWithLayersAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(id == schedule.Id ? schedule : null);
        public Task<OnCallSchedule> CreateAsync(OnCallSchedule s, CancellationToken ct = default) => Task.FromResult(s);
        public Task UpdateAsync(OnCallSchedule s, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<OnCallLayer> CreateLayerAsync(OnCallLayer l, CancellationToken ct = default) => Task.FromResult(l);
        public Task<OnCallLayer> UpdateLayerAsync(OnCallLayer l, CancellationToken ct = default) => Task.FromResult(l);
        public Task DeleteLayerAsync(int layerId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<OnCallOverride> CreateOverrideAsync(OnCallOverride ov, CancellationToken ct = default) => Task.FromResult(ov);
        public Task DeleteOverrideAsync(int overrideId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static AppUser MakeUser(int id, string name) => new() { Id = id, Name = name, UserName = name, Email = $"{name}@test.local", Color = "#000000" };

    [Fact]
    public async Task GetOnCallUsersAt_WeeklyRotationWithShortShift_UsesOccurrenceIndexNotElapsedTime()
    {
        // Weekly rotation, 8h shift each Monday, 3 users round-robin.
        // A shift duration much shorter than the 7-day recurrence interval previously caused
        // elapsedIntervals to be computed from `duration` instead of the RRULE occurrence index,
        // desynchronizing GetCurrentOnCallUsersAsync from ExpandScheduleAsync.
        var userA = MakeUser(1, "alice");
        var userB = MakeUser(2, "bob");
        var userC = MakeUser(3, "carol");

        var firstStart = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero); // Monday
        var firstEnd = firstStart.AddHours(8);

        var layer = new OnCallLayer
        {
            Id = 1,
            ScheduleId = 1,
            Name = "Primary",
            Order = 0,
            RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO",
            FirstOccurrenceStartsAt = firstStart,
            FirstOccurrenceEndsAt = firstEnd,
            Users =
            [
                new OnCallLayerUser { Id = 1, UserId = 1, User = userA, Position = 0 },
                new OnCallLayerUser { Id = 2, UserId = 2, User = userB, Position = 1 },
                new OnCallLayerUser { Id = 3, UserId = 3, User = userC, Position = 2 },
            ],
        };

        var schedule = new OnCallSchedule
        {
            Id = 1,
            Name = "Test",
            Layers = [layer],
            Overrides = [],
        };

        var service = new OnCallService(new FakeScheduleRepository(schedule), new RRuleExpander());

        // 3rd Monday shift (index 2) → carol, per 0-based round robin.
        var thirdShift = firstStart.AddDays(14).AddHours(1); // well inside the 8h window

        var onCall = await service.GetOnCallUsersAtAsync(1, thirdShift);

        onCall.Should().ContainSingle();
        onCall[0].Id.Should().Be(userC.Id);
    }

    [Fact]
    public async Task GetOnCallUsersAt_MatchesExpandSchedule_ForSameInstant()
    {
        var userA = MakeUser(1, "alice");
        var userB = MakeUser(2, "bob");

        var firstStart = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
        var firstEnd = firstStart.AddHours(8);

        var layer = new OnCallLayer
        {
            Id = 1,
            ScheduleId = 1,
            Name = "Primary",
            Order = 0,
            RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO",
            FirstOccurrenceStartsAt = firstStart,
            FirstOccurrenceEndsAt = firstEnd,
            Users =
            [
                new OnCallLayerUser { Id = 1, UserId = 1, User = userA, Position = 0 },
                new OnCallLayerUser { Id = 2, UserId = 2, User = userB, Position = 1 },
            ],
        };

        var schedule = new OnCallSchedule { Id = 1, Name = "Test", Layers = [layer], Overrides = [] };
        var service = new OnCallService(new FakeScheduleRepository(schedule), new RRuleExpander());

        var atInstant = firstStart.AddDays(35).AddHours(2); // 5th Monday, inside shift window

        var current = await service.GetOnCallUsersAtAsync(1, atInstant);
        var expanded = await service.ExpandScheduleAsync(1, atInstant.AddMinutes(-30), atInstant.AddMinutes(30), applyOverrides: false);

        var expandedUserId = expanded.Should().ContainSingle().Subject.UserId;
        current.Should().ContainSingle().Subject.Id.Should().Be(expandedUserId);
    }
}

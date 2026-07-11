using System.Threading.Channels;
using FluentAssertions;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.UnitTests;

public class MaintenanceAppServiceTests
{
    private class FakeMaintenanceRepository : IMaintenanceRepository
    {
        public List<MaintenanceEvent> AddedEvents { get; } = [];

        public Task<IEnumerable<Maintenance>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<Maintenance>());
        public Task<IEnumerable<Maintenance>> GetAllForPublicAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<Maintenance>());
        public Task<Maintenance?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<Maintenance?>(null);
        public Task<IEnumerable<Maintenance>> GetActiveAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<Maintenance>());
        public Task<Maintenance> CreateAsync(Maintenance maintenance, CancellationToken ct = default) => Task.FromResult(maintenance);
        public Task<Maintenance> UpdateAsync(Maintenance maintenance, CancellationToken ct = default) => Task.FromResult(maintenance);
        public Task DeleteAsync(Maintenance maintenance, CancellationToken ct = default) => Task.CompletedTask;

        public Task AddEventsAsync(IEnumerable<MaintenanceEvent> events, CancellationToken ct = default)
        {
            AddedEvents.AddRange(events);
            return Task.CompletedTask;
        }

        public Task DeleteFutureEventsAsync(int maintenanceId, long fromTimestamp, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<MaintenanceEvent>> GetActiveEventsAsync(CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<MaintenanceEvent>());
        public Task<bool> HasActiveWindowAsync(int serviceId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<int>> GetAffectedServiceIdsAsync(int maintenanceId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>([]);
        public Task<MaintenanceEvent?> GetEventByIdAsync(int maintenanceId, int eventId, CancellationToken ct = default) => Task.FromResult<MaintenanceEvent?>(null);
        public Task CancelEventAsync(MaintenanceEvent maintenanceEvent, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FixedOccurrenceExpander(DateTime occurrence) : IRRuleExpander
    {
        public IEnumerable<DateTime> GetOccurrences(DateTime dtStart, string rRule, DateTime from, DateTime to) => [occurrence];
    }

    [Fact]
    public async Task MaterializeEventsAsync_OccurrenceWindowFullyElapsed_MarksCompletedNotOngoing()
    {
        // Regression: an occurrence whose start AND end are both in the past (e.g. re-materialized
        // during a horizon extension) was being stamped Ongoing because the status calculation only
        // compared `start <= now`, never the end of the window.
        var now = DateTime.UtcNow;
        var pastStart = now.AddHours(-2);
        var maintenance = new Maintenance
        {
            Id = 1,
            RRule = "FREQ=DAILY",
            StartDateTime = new DateTimeOffset(pastStart, TimeSpan.Zero).ToUnixTimeSeconds(),
            DurationSeconds = 3600, // 1 hour — window ended an hour ago
        };

        var repo = new FakeMaintenanceRepository();
        var service = new MaintenanceAppService(
            repo,
            serviceRepo: null!,
            rruleExpander: new FixedOccurrenceExpander(pastStart),
            statusChannel: Channel.CreateUnbounded<CheckStatusChangedEvent>());

        await service.MaterializeEventsAsync(maintenance);

        repo.AddedEvents.Should().ContainSingle()
            .Which.Status.Should().Be(MaintenanceEventStatus.Completed);
    }

    [Fact]
    public async Task MaterializeEventsAsync_OccurrenceCurrentlyInWindow_MarksOngoing()
    {
        var now = DateTime.UtcNow;
        var currentStart = now.AddMinutes(-10);
        var maintenance = new Maintenance
        {
            Id = 1,
            RRule = "FREQ=DAILY",
            StartDateTime = new DateTimeOffset(currentStart, TimeSpan.Zero).ToUnixTimeSeconds(),
            DurationSeconds = 3600, // window ends in 50 minutes
        };

        var repo = new FakeMaintenanceRepository();
        var service = new MaintenanceAppService(
            repo,
            serviceRepo: null!,
            rruleExpander: new FixedOccurrenceExpander(currentStart),
            statusChannel: Channel.CreateUnbounded<CheckStatusChangedEvent>());

        await service.MaterializeEventsAsync(maintenance);

        repo.AddedEvents.Should().ContainSingle()
            .Which.Status.Should().Be(MaintenanceEventStatus.Ongoing);
    }
}

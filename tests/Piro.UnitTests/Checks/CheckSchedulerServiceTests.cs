using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Infrastructure.Jobs;
using Quartz;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Covers the RFC 0019 §4.5 scheduler fix: deactivating a check must remove its trigger. Previously
/// ScheduleAsync returned early on an inactive check, so toggling IsActive to false left the existing
/// trigger in place and firing — an apparent success that changed nothing.
/// </summary>
public class CheckSchedulerServiceTests
{
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private readonly CheckSchedulerService _sut;

    public CheckSchedulerServiceTests()
    {
        var factory = Substitute.For<ISchedulerFactory>();
        factory.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _sut = new CheckSchedulerService(
            factory,
            Substitute.For<ICheckRepository>(),
            NullLogger<CheckSchedulerService>.Instance);
    }

    private static Check Check(bool isActive) =>
        new() { Id = 42, ServiceId = 1, Slug = "c", Name = "C", Cron = "* * * * *", IsActive = isActive };

    [Fact]
    public async Task InactiveCheck_HasItsJobDeleted()
    {
        await _sut.ScheduleAsync(Check(isActive: false));

        await _scheduler.Received(1).DeleteJob(
            Arg.Is<JobKey>(k => k.Name == "check-42" && k.Group == "checks"),
            Arg.Any<CancellationToken>());
        await _scheduler.DidNotReceive().ScheduleJob(
            Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActiveCheck_IsScheduledAndNotDeleted()
    {
        _scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(false);

        await _sut.ScheduleAsync(Check(isActive: true));

        await _scheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
        await _scheduler.DidNotReceive().DeleteJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>());
    }
}

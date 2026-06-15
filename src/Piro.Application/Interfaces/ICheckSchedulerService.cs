using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Registers and removes Quartz jobs in response to check CRUD operations.</summary>
public interface ICheckSchedulerService
{
    /// <summary>Schedules (or reschedules) a Quartz job for the given check.</summary>
    Task ScheduleAsync(Check check, CancellationToken ct = default);

    /// <summary>Removes the Quartz job for the given check, if it exists.</summary>
    Task UnscheduleAsync(int checkId, CancellationToken ct = default);

    /// <summary>Reads all active checks from the DB and registers their jobs.  Called once at startup.</summary>
    Task InitializeFromDatabaseAsync(CancellationToken ct = default);
}

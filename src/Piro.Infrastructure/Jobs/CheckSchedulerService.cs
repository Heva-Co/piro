using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Quartz;

namespace Piro.Infrastructure.Jobs;

/// <summary>Quartz-backed implementation of <see cref="ICheckSchedulerService"/>.</summary>
internal class CheckSchedulerService(
    ISchedulerFactory schedulerFactory,
    ICheckRepository checkRepo,
    ILogger<CheckSchedulerService> logger) : ICheckSchedulerService
{
    public async Task ScheduleAsync(Check check, CancellationToken ct = default)
    {
        if (!check.IsActive) return;

        var scheduler = await schedulerFactory.GetScheduler(ct);

        var jobKey = JobKey(check.Id);
        var triggerKey = TriggerKey(check.Id);

        var job = JobBuilder.Create<CheckExecutionJob>()
            .WithIdentity(jobKey)
            .UsingJobData(CheckExecutionJob.CheckIdKey, check.Id.ToString())
            .StoreDurably()
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithCronSchedule(ToQuartzCron(check.Cron))
            .Build();

        if (await scheduler.CheckExists(jobKey, ct))
        {
            await scheduler.RescheduleJob(triggerKey, trigger, ct);
            logger.LogDebug("Rescheduled job for check {CheckId} with cron '{Cron}'.", check.Id, check.Cron);
        }
        else
        {
            await scheduler.ScheduleJob(job, trigger, ct);
            logger.LogDebug("Scheduled job for check {CheckId} with cron '{Cron}'.", check.Id, check.Cron);
        }
    }

    public async Task UnscheduleAsync(int checkId, CancellationToken ct = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var deleted = await scheduler.DeleteJob(JobKey(checkId), ct);
        if (deleted)
            logger.LogDebug("Unscheduled job for check {CheckId}.", checkId);
    }

    public async Task InitializeFromDatabaseAsync(CancellationToken ct = default)
    {
        var checks = await checkRepo.GetAllActiveAsync(ct);
        foreach (var check in checks)
            await ScheduleAsync(check, ct);

        logger.LogInformation("Scheduler initialized with {Count} active check(s).", checks.Count());
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Quartz.JobKey JobKey(int checkId) => new($"check-{checkId}", "checks");
    private static Quartz.TriggerKey TriggerKey(int checkId) => new($"trigger-{checkId}", "checks");

    /// <summary>
    /// Converts a standard 5-field cron expression to Quartz 6-field format (prepends seconds=0).
    /// If already 6 fields, returns as-is.
    /// </summary>
    private static string ToQuartzCron(string cron)
    {
        var parts = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 6) return cron;

        // Standard 5-field: min hour dom month dow
        // Quartz 6-field:   sec min hour dom month dow
        // When day-of-week is '*', Quartz requires '?' when day-of-month is also '*'
        var dow = parts[4] == "*" ? "?" : parts[4];
        return $"0 {parts[0]} {parts[1]} {parts[2]} {parts[3]} {dow}";
    }
}

using Piro.Application.Interfaces;
using Quartz;

namespace Piro.Infrastructure.Jobs;

/// <summary>Quartz-backed implementation of <see cref="IIncidentPublishScheduler"/>.</summary>
internal class IncidentPublishScheduler(ISchedulerFactory schedulerFactory) : IIncidentPublishScheduler
{
    public async Task ScheduleAsync(int incidentId, int delayMinutes, CancellationToken ct = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var jobKey = JobKey(incidentId);
        var triggerKey = TriggerKey(incidentId);

        var job = JobBuilder.Create<PublishIncidentJob>()
            .WithIdentity(jobKey)
            .UsingJobData(PublishIncidentJob.IncidentIdKey, incidentId.ToString())
            .StoreDurably()
            .Build();

        var fireAt = DateTimeOffset.UtcNow.AddMinutes(delayMinutes);
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .StartAt(fireAt)
            .Build();

        if (await scheduler.CheckExists(jobKey, ct))
            await scheduler.RescheduleJob(triggerKey, trigger, ct);
        else
            await scheduler.ScheduleJob(job, trigger, ct);
    }

    public async Task ExtendAsync(int incidentId, int additionalMinutes, CancellationToken ct = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var triggerKey = TriggerKey(incidentId);

        var existing = await scheduler.GetTrigger(triggerKey, ct);
        var baseTime = existing?.GetNextFireTimeUtc() ?? DateTimeOffset.UtcNow;
        var newFireAt = baseTime.AddMinutes(additionalMinutes);

        if (existing is not null)
        {
            var updated = existing.GetTriggerBuilder()
                .StartAt(newFireAt)
                .Build();
            await scheduler.RescheduleJob(triggerKey, updated, ct);
        }
        else
        {
            // No existing job — schedule fresh from now
            await ScheduleAsync(incidentId, additionalMinutes, ct);
        }
    }

    public async Task CancelAsync(int incidentId, CancellationToken ct = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        await scheduler.DeleteJob(JobKey(incidentId), ct);
    }

    public async Task<DateTimeOffset?> GetScheduledTimeAsync(int incidentId, CancellationToken ct = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var trigger = await scheduler.GetTrigger(TriggerKey(incidentId), ct);
        return trigger?.GetNextFireTimeUtc();
    }

    private static JobKey JobKey(int incidentId) => new($"publish-incident-{incidentId}", "incident-publish");
    private static TriggerKey TriggerKey(int incidentId) => new($"trigger-publish-incident-{incidentId}", "incident-publish");
}

using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Quartz;
using Quartz.Impl.Matchers;

namespace Piro.Infrastructure.Jobs;

/// <summary>Quartz-backed implementation of <see cref="IJobStatusService"/>.</summary>
internal class JobStatusService(ISchedulerFactory schedulerFactory, ICheckRepository checkRepo) : IJobStatusService
{
    private const string ChecksGroup = "checks";
    private const string ChecksJobPrefix = "check-";

    public async Task<IEnumerable<JobStatusDto>> GetAllAsync(CancellationToken ct = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);

        var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), ct);
        var checksById = (await checkRepo.GetAllWithServiceAsync(ct)).ToDictionary(c => c.Id);

        var result = new List<JobStatusDto>();

        foreach (var jobKey in jobKeys)
        {
            var triggers = await scheduler.GetTriggersOfJob(jobKey, ct);
            var check = TryResolveCheck(jobKey, checksById);

            foreach (var trigger in triggers)
            {
                var state = await scheduler.GetTriggerState(trigger.Key, ct);
                result.Add(new JobStatusDto(
                    jobKey.Group,
                    jobKey.Name,
                    trigger.Key.Group,
                    trigger.Key.Name,
                    state.ToString(),
                    trigger.GetNextFireTimeUtc(),
                    trigger.GetPreviousFireTimeUtc(),
                    check is null ? null : new CheckRefDto(check.Id, check.Name, check.Slug, check.Service.Slug)));
            }
        }

        return result;
    }

    private static Domain.Entities.Check? TryResolveCheck(
        JobKey jobKey, Dictionary<int, Domain.Entities.Check> checksById)
    {
        if (jobKey.Group != ChecksGroup || !jobKey.Name.StartsWith(ChecksJobPrefix))
            return null;

        return int.TryParse(jobKey.Name[ChecksJobPrefix.Length..], out var checkId) &&
               checksById.TryGetValue(checkId, out var check)
            ? check
            : null;
    }
}

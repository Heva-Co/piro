namespace Piro.Application.Interfaces;

/// <summary>Schedules, reschedules, and cancels the automatic publication of draft incidents.</summary>
public interface IIncidentPublishScheduler
{
    /// <summary>Schedules a one-shot job to publish the incident after <paramref name="delayMinutes"/> minutes.</summary>
    Task ScheduleAsync(int incidentId, int delayMinutes, CancellationToken ct = default);

    /// <summary>
    /// Extends the existing publish timer by <paramref name="additionalMinutes"/> minutes.
    /// If no job is scheduled, schedules a new one from now.
    /// </summary>
    Task ExtendAsync(int incidentId, int additionalMinutes, CancellationToken ct = default);

    /// <summary>Cancels the scheduled publish job, leaving the incident as a draft indefinitely.</summary>
    Task CancelAsync(int incidentId, CancellationToken ct = default);

    /// <summary>Returns when the incident is scheduled to be published, or null if no job is pending.</summary>
    Task<DateTimeOffset?> GetScheduledTimeAsync(int incidentId, CancellationToken ct = default);
}

using Piro.Application.Interfaces;
using Quartz;

namespace Piro.Infrastructure.Jobs;

/// <summary>
/// <see cref="ICronIntervalCalculator"/> over Quartz's cron engine — samples the next few fire
/// times of a cron and returns the smallest gap between consecutive fires (RFC 0011). Keeps the
/// Quartz dependency out of the Application layer that validates interval floors.
/// </summary>
internal sealed class QuartzCronIntervalCalculator : ICronIntervalCalculator
{
    /// <summary>How many consecutive fires to sample when measuring the tightest cadence.</summary>
    private const int SampleFires = 12;

    public TimeSpan? SmallestInterval(string cron)
    {
        CronExpression expression;
        try
        {
            expression = new CronExpression(QuartzCron.ToQuartzCron(cron));
        }
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentException)
        {
            return null; // malformed cron (bad field count or Quartz parse failure) — unvalidatable
        }

        // Walk forward from a fixed anchor, collecting fire times, and track the smallest gap.
        // A fixed anchor keeps this deterministic and independent of wall-clock.
        var cursor = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan? smallest = null;
        var previous = expression.GetTimeAfter(cursor);
        if (previous is null) return null;

        for (var i = 0; i < SampleFires; i++)
        {
            var next = expression.GetTimeAfter(previous.Value);
            if (next is null) break;

            var gap = next.Value - previous.Value;
            if (smallest is null || gap < smallest) smallest = gap;
            previous = next;
        }

        return smallest;
    }
}

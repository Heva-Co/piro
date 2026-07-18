namespace Piro.Infrastructure.Jobs;

/// <summary>Shared cron helpers over Quartz's 6-field format, used by both the scheduler and the interval calculator.</summary>
internal static class QuartzCron
{
    /// <summary>
    /// Converts a standard 5-field cron expression to Quartz 6-field format (prepends seconds=0).
    /// If already 6 fields, returns as-is. When day-of-week is '*', Quartz requires '?'.
    /// </summary>
    public static string ToQuartzCron(string cron)
    {
        var parts = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 6) return cron;

        // Standard 5-field: min hour dom month dow → Quartz 6-field: sec min hour dom month dow
        var dow = parts[4] == "*" ? "?" : parts[4];
        return $"0 {parts[0]} {parts[1]} {parts[2]} {parts[3]} {dow}";
    }
}

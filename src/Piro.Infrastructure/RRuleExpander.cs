using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Piro.Application.Interfaces;

namespace Piro.Infrastructure;

/// <summary>Ical.Net-backed implementation of <see cref="IRRuleExpander"/>.</summary>
public class RRuleExpander : IRRuleExpander
{
    public IEnumerable<DateTime> GetOccurrences(DateTime dtStart, string rRule, DateTime from, DateTime to)
    {
        try
        {
            var calEvent = new CalendarEvent
            {
                DtStart = new CalDateTime(dtStart, "UTC"),
                RecurrenceRules = [new RecurrencePattern(rRule)]
            };

            // TakeWhile prevents unbounded iteration for infinite RRULEs (no COUNT/UNTIL).
            return calEvent.GetOccurrences()
                .Select(o => o.Period.StartTime.Value)
                .TakeWhile(d => d <= to)
                .Where(d => d >= from)
                .ToList();
        }
        catch
        {
            // Invalid or unrecognized RRULE — fall back to the single first occurrence.
            return dtStart >= from && dtStart <= to ? [dtStart] : [];
        }
    }
}

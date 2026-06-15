namespace Piro.Application.Interfaces;

/// <summary>Expands an iCalendar RRULE into concrete occurrence timestamps.</summary>
public interface IRRuleExpander
{
    /// <summary>
    /// Returns UTC <see cref="DateTime"/> occurrences within [<paramref name="from"/>, <paramref name="to"/>].
    /// The <paramref name="dtStart"/> is the DTSTART of the recurrence rule.
    /// Returns a single occurrence at <paramref name="dtStart"/> if the RRULE is invalid or empty.
    /// </summary>
    IEnumerable<DateTime> GetOccurrences(DateTime dtStart, string rRule, DateTime from, DateTime to);
}

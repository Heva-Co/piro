using Microsoft.AspNetCore.Mvc;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Enums;

namespace Piro.Api.Controllers;

/// <summary>Exposes read-only status data for the public status page. No authentication required.</summary>
/// <remarks>
/// Never exposes check details, check_data_points, or detailed propagation sources.
/// Only the computed service status is visible publicly.
/// </remarks>
[ApiController]
[Route("api/v1/public")]
[Produces("application/json")]
public class PublicController(
    IServiceRepository serviceRepo,
    ICheckDataPointRepository dataPointRepo,
    IIncidentRepository incidentRepo) : ControllerBase
{
    /// <summary>Returns all visible services with their current computed status.</summary>
    [HttpGet("services")]
    [ProducesResponseType<IEnumerable<PublicServiceDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServices(CancellationToken ct)
    {
        var services = await serviceRepo.GetAllAsync(ct);
        var result = services
            .Where(s => !s.IsHidden)
            .Select(s => new PublicServiceDto(
                s.Slug, s.Name, s.Description, s.ImageUrl,
                s.CurrentStatus, s.DisplayOrder,
                s.HistoryDaysDesktop, s.HistoryDaysMobile));
        return Ok(result);
    }

    /// <summary>Returns a single visible service with its current computed status.</summary>
    [HttpGet("services/{slug}")]
    [ProducesResponseType<PublicServiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetService(string slug, CancellationToken ct)
    {
        var service = await serviceRepo.GetBySlugAsync(slug, ct);
        if (service is null || service.IsHidden)
            return NotFound();

        return Ok(new PublicServiceDto(
            service.Slug, service.Name, service.Description, service.ImageUrl,
            service.CurrentStatus, service.DisplayOrder,
            service.HistoryDaysDesktop, service.HistoryDaysMobile));
    }

    /// <summary>Returns uptime percentage for a service over the last <c>days</c> days (default 30).</summary>
    [HttpGet("services/{slug}/uptime")]
    [ProducesResponseType<PublicUptimeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUptime(
        string slug,
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        if (days < 1 || days > 365)
            return BadRequest(new { title = "days must be between 1 and 365.", status = 400 });

        var service = await serviceRepo.GetBySlugAsync(slug, ct);
        if (service is null || service.IsHidden)
            return NotFound();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var from = now - (long)days * 86400;

        var allIncidents = await incidentRepo.GetAllPublicAsync(includeResolved: true, ct);
        var relevant = allIncidents
            .Where(i => i.StartDateTime < now && (i.EndDateTime ?? now) > from)
            .ToList();

        // Count incident-covered minutes as non-UP; everything else is UP
        long totalMinutes = (now - from) / 60;
        long nonUpMinutes = 0;

        foreach (var inc in relevant)
        {
            var incStart = Math.Max(inc.StartDateTime, from);
            var incEnd   = Math.Min(inc.EndDateTime ?? now, now);
            if (incEnd > incStart)
                nonUpMinutes += (incEnd - incStart) / 60;
        }

        // Clamp — overlapping incidents could theoretically double-count; cap at total
        nonUpMinutes = Math.Min(nonUpMinutes, totalMinutes);
        var upMinutes = totalMinutes - nonUpMinutes;
        var percent = totalMinutes == 0 ? 100.0 : Math.Round((double)upMinutes / totalMinutes * 100, 4);

        return Ok(new PublicUptimeDto(service.Slug, days, percent, totalMinutes, upMinutes));
    }

    /// <summary>
    /// Returns per-day aggregated status and latency data for the service detail page.
    /// Query param: <c>days</c> (1–90, default 30).
    /// </summary>
    [HttpGet("services/{slug}/overview")]
    [ProducesResponseType<ServiceOverviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOverview(
        string slug,
        [FromQuery] int? days = null,
        CancellationToken ct = default)
    {
        var service = await serviceRepo.GetBySlugAsync(slug, ct);
        if (service is null || service.IsHidden)
            return NotFound();

        var effectiveDays = Math.Clamp(days ?? service.HistoryDaysDesktop, 1, service.HistoryDaysDesktop);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var from = now - (long)effectiveDays * 86400;

        var dailyLatency = await dataPointRepo.GetDailyLatencyByServiceIdAsync(service.Id, from, now, ct);
        var latest = await dataPointRepo.GetLatestByServiceIdAsync(service.Id, ct);

        // Incident-based day counts
        var allIncidents = await incidentRepo.GetAllPublicAsync(includeResolved: true, ct);
        var relevantIncidents = allIncidents
            .Where(i => i.StartDateTime < now && (i.EndDateTime ?? now) > from)
            .ToList();

        var latencyByDay = dailyLatency.ToDictionary(d => d.DayTimestamp);

        static int ImpactRank(ServiceStatus s) => s switch
        {
            ServiceStatus.MAINTENANCE => 1,
            ServiceStatus.DEGRADED    => 2,
            ServiceStatus.DOWN        => 3,
            _                         => 0,
        };

        var dailyData = new List<DailyStatsDto>();
        var dayStart = (from / 86400) * 86400;
        var dayEnd = (now / 86400) * 86400;
        long totalMinutes = 0, upMinutes = 0;

        for (var day = dayStart; day <= dayEnd; day += 86400)
        {
            var dStart = day;
            var dEnd = day + 86400;
            // Cap today at current time so future minutes don't count
            var dKnownEnd = Math.Min(dEnd, now);
            var knownMinutes = (int)((dKnownEnd - dStart) / 60);

            // Per-minute worst-impact array for this day
            var statuses = new ServiceStatus[knownMinutes];
            Array.Fill(statuses, ServiceStatus.UP);

            foreach (var inc in relevantIncidents)
            {
                if (inc.StartDateTime >= dEnd || (inc.EndDateTime ?? dEnd) <= dStart) continue;

                var changes = inc.ImpactChanges.OrderBy(c => c.Timestamp).ToList();
                var incStart = Math.Max(inc.StartDateTime, dStart);
                var incEnd   = Math.Min(inc.EndDateTime ?? dKnownEnd, dKnownEnd);

                var mStart = (int)((incStart - dStart) / 60);
                var mEnd   = (int)Math.Ceiling((double)(incEnd - dStart) / 60);
                mStart = Math.Clamp(mStart, 0, knownMinutes - 1);
                mEnd   = Math.Clamp(mEnd,   0, knownMinutes);

                for (var m = mStart; m < mEnd; m++)
                {
                    var minuteTs = dStart + m * 60L;
                    ServiceStatus impact;
                    if (changes.Count == 0)
                    {
                        impact = inc.CurrentImpact;
                    }
                    else
                    {
                        var active = changes.LastOrDefault(c => c.Timestamp <= minuteTs);
                        impact = active is not null ? active.Impact : changes[0].Impact;
                    }
                    if (ImpactRank(impact) > ImpactRank(statuses[m]))
                        statuses[m] = impact;
                }
            }

            var cUp          = statuses.Count(s => s == ServiceStatus.UP);
            var cDown        = statuses.Count(s => s == ServiceStatus.DOWN);
            var cDegraded    = statuses.Count(s => s == ServiceStatus.DEGRADED);
            var cMaintenance = statuses.Count(s => s == ServiceStatus.MAINTENANCE);

            // Days with no known minutes yet (future days) show as no-data
            if (knownMinutes == 0) cUp = 0;

            latencyByDay.TryGetValue(day, out var lat);
            dailyData.Add(new DailyStatsDto(
                day,
                cUp, cDown, cDegraded, cMaintenance,
                lat == default ? null : lat.Avg,
                lat == default ? null : lat.Min,
                lat == default ? null : lat.Max
            ));

            totalMinutes += knownMinutes;
            upMinutes    += cUp;
        }

        var uptimePercent = totalMinutes == 0 ? 100.0 : Math.Round((double)upMinutes / totalMinutes * 100, 4);

        // Overall latency
        var allLatencies = dailyLatency.ToList();
        double? overallAvg = allLatencies.Count > 0 ? allLatencies.Average(d => d.Avg) : null;
        double? overallMin = allLatencies.Count > 0 ? allLatencies.Min(d => d.Min) : null;
        double? overallMax = allLatencies.Count > 0 ? allLatencies.Max(d => d.Max) : null;

        return Ok(new ServiceOverviewDto(
            service.Slug, service.Name, service.Description, service.ImageUrl,
            service.CurrentStatus,
            latest?.Timestamp ?? now,
            latest?.LatencyMs,
            uptimePercent,
            overallAvg, overallMin, overallMax,
            from, now,
            dailyData
        ));
    }

    /// <summary>
    /// Returns 1440 minute-aligned status entries for a single day.
    /// All minutes default to UP; minutes covered by a public incident are set to the incident's impact at that minute.
    /// Query param: <c>date</c> as Unix timestamp of the day's 00:00:00 UTC (seconds).
    /// </summary>
    [HttpGet("services/{slug}/day-status")]
    [ProducesResponseType<IEnumerable<PublicStatusPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDayStatus(
        string slug,
        [FromQuery] long date,
        CancellationToken ct)
    {
        var service = await serviceRepo.GetBySlugAsync(slug, ct);
        if (service is null || service.IsHidden)
            return NotFound();

        var dayStart = date;
        var dayEnd = date + 86400;

        // All incidents that overlap this day (public, not merged)
        var allIncidents = await incidentRepo.GetAllPublicAsync(includeResolved: true, ct);
        var dayIncidents = allIncidents
            .Where(i => i.StartDateTime < dayEnd && (i.EndDateTime ?? dayEnd) > dayStart)
            .ToList();

        // Minutes beyond current time are NO_DATA; past minutes default to UP
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var lastKnownMinute = (int)Math.Min((now - dayStart) / 60, 1439);

        var statuses = new ServiceStatus[1440];
        for (var m = 0; m < 1440; m++)
            statuses[m] = m <= lastKnownMinute ? ServiceStatus.UP : ServiceStatus.NO_DATA;

        static int ImpactRank(ServiceStatus s) => s switch
        {
            ServiceStatus.MAINTENANCE => 1,
            ServiceStatus.DEGRADED    => 2,
            ServiceStatus.DOWN        => 3,
            _                         => 0,
        };

        foreach (var inc in dayIncidents)
        {
            var changes = inc.ImpactChanges
                .OrderBy(c => c.Timestamp)
                .ToList();

            var incStart = Math.Max(inc.StartDateTime, dayStart);
            var incEnd   = Math.Min(inc.EndDateTime ?? dayEnd, dayEnd);

            var startMinute = (int)((incStart - dayStart) / 60);
            var endMinute   = (int)Math.Ceiling((double)(incEnd - dayStart) / 60);
            startMinute = Math.Clamp(startMinute, 0, 1439);
            endMinute   = Math.Clamp(endMinute,   0, lastKnownMinute + 1);

            for (var m = startMinute; m < endMinute; m++)
            {
                var minuteTs = dayStart + m * 60L;

                // Find the impact at this minute: last change at or before minuteTs
                ServiceStatus impact;
                if (changes.Count == 0)
                {
                    impact = inc.CurrentImpact;
                }
                else
                {
                    var active = changes.LastOrDefault(c => c.Timestamp <= minuteTs);
                    impact = active is not null ? active.Impact : changes[0].Impact;
                }

                if (ImpactRank(impact) > ImpactRank(statuses[m]))
                    statuses[m] = impact;
            }
        }

        var result = Enumerable.Range(0, 1440)
            .Select(m => new PublicStatusPointDto(dayStart + m * 60L, statuses[m]));

        return Ok(result);
    }
}

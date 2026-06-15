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
    IServiceStatusSnapshotRepository snapshotRepo,
    ICheckDataPointRepository dataPointRepo) : ControllerBase
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

    /// <summary>
    /// Returns minute-aligned status history for a service.
    /// Query params: <c>from</c> and <c>to</c> as Unix timestamps (seconds). Defaults to last 24 hours.
    /// </summary>
    [HttpGet("services/{slug}/history")]
    [ProducesResponseType<IEnumerable<PublicStatusPointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(
        string slug,
        [FromQuery] long? from,
        [FromQuery] long? to,
        CancellationToken ct)
    {
        var service = await serviceRepo.GetBySlugAsync(slug, ct);
        if (service is null || service.IsHidden)
            return NotFound();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var fromTs = from ?? now - 86400; // last 24h
        var toTs = to ?? now;

        var snapshots = await snapshotRepo.GetByServiceIdAsync(service.Id, fromTs, toTs, ct);
        var result = snapshots
            .OrderBy(s => s.Timestamp)
            .Select(s => new PublicStatusPointDto(s.Timestamp, s.ComputedStatus));

        return Ok(result);
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

        var snapshots = await snapshotRepo.GetByServiceIdAsync(service.Id, from, now, ct);
        var list = snapshots.ToList();

        var total = list.Count;
        var up = list.Count(s => s.ComputedStatus == ServiceStatus.UP);

        var percent = total == 0 ? 100.0 : Math.Round((double)up / total * 100, 4);

        return Ok(new PublicUptimeDto(service.Slug, days, percent, total, up));
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

        var dailyCounts = await snapshotRepo.GetDailyCountsAsync(service.Id, from, now, ct);
        var dailyLatency = await dataPointRepo.GetDailyLatencyByServiceIdAsync(service.Id, from, now, ct);
        var latest = await dataPointRepo.GetLatestByServiceIdAsync(service.Id, ct);

        // Fill all days in range — include empty days so the bar chart shows N bars
        var countByDay = dailyCounts.ToDictionary(d => d.DayTimestamp);
        var latencyByDay = dailyLatency.ToDictionary(d => d.DayTimestamp);

        var dailyData = new List<DailyStatsDto>();
        var dayStart = (from / 86400) * 86400;
        var dayEnd = (now / 86400) * 86400;
        for (var day = dayStart; day <= dayEnd; day += 86400)
        {
            countByDay.TryGetValue(day, out var cnt);
            latencyByDay.TryGetValue(day, out var lat);
            dailyData.Add(new DailyStatsDto(
                day,
                cnt.CountUp, cnt.CountDown, cnt.CountDegraded, cnt.CountMaintenance,
                lat == default ? null : lat.Avg,
                lat == default ? null : lat.Min,
                lat == default ? null : lat.Max
            ));
        }

        // Overall uptime
        var totalSnaps = dailyCounts.Sum(d => d.CountUp + d.CountDown + d.CountDegraded + d.CountMaintenance);
        var upSnaps = dailyCounts.Sum(d => d.CountUp);
        var uptimePercent = totalSnaps == 0 ? 100.0 : Math.Round((double)upSnaps / totalSnaps * 100, 4);

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
}

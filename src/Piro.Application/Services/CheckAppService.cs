using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Checks.Config;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;
using Piro.Domain.Extensions;

namespace Piro.Application.Services;

/// <summary>Application service for check CRUD operations within a service.</summary>
/// <remarks>
/// Every check must belong to an existing service. Slug is unique within the parent service.
/// Scheduling side effects (Quartz job registration) are handled separately by
/// <see cref="CheckSchedulerService"/> and called from the controller after persisting.
/// </remarks>
public class CheckAppService(
    ICheckRepository checkRepository,
    IServiceRepository serviceRepository,
    ICheckSchedulerService scheduler,
    ICheckDataPointRepository dataPointRepository,
    IAlertConfigRepository alertConfigRepository,
    ICronIntervalCalculator cronInterval,
    IUnitOfWork unitOfWork)
{
    public async Task<IEnumerable<CheckSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await checkRepository.GetAllWithServiceAndLastErrorAsync(ct);
        return rows.Select(r => new CheckSummaryDto(
            r.Check.Id, r.Check.Service.Slug, r.Check.Service.Name,
            r.Check.Slug, r.Check.Name, r.Check.Description,
            r.Check.Type, r.Check.Cron, r.Check.CurrentStatus,
            r.Check.IsActive, r.Check.IsMultiRegion, r.Check.UpdatedAt, r.LastErrorMessage));
    }

    public async Task<IEnumerable<CheckDto>> GetByServiceSlugAsync(string serviceSlug, CancellationToken ct = default)
    {
        var service = await serviceRepository.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);
        var checks = await checkRepository.GetByServiceIdAsync(service.Id, ct);
        return checks.Select(c => c.ToDto());
    }

    public async Task<CheckDto> GetBySlugAsync(string serviceSlug, string checkSlug, CancellationToken ct = default)
    {
        var service = await serviceRepository.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);
        var check = await checkRepository.GetBySlugAsync(service.Id, checkSlug, ct)
            ?? throw new NotFoundException(nameof(Check), checkSlug);
        return check.ToDto();
    }

    public async Task<CheckDto> CreateAsync(string serviceSlug, CreateCheckRequest request, CancellationToken ct = default)
    {
        var service = await serviceRepository.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);

        if (await checkRepository.SlugExistsInServiceAsync(service.Id, request.Slug, ct))
            throw new DomainValidationException($"A check with slug '{request.Slug}' already exists in service '{serviceSlug}'.");

        foreach (var alertConfigRequest in request.AlertConfigs ?? [])
            EnsureAlertForAllowed(request.Type, alertConfigRequest.AlertFor);

        EnsureScheduleWithinBounds(request.Type, request.Cron, request.TypeDataJson);

        var check = new Check
        {
            ServiceId = service.Id,
            Slug = request.Slug,
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            Cron = request.Cron,
            TypeDataJson = request.TypeDataJson,
            CurrentStatus = ServiceStatus.NO_DATA,
            IsActive = request.IsActive,
            IsMultiRegion = request.IsMultiRegion,
            IntegrationId = request.IntegrationId
        };

        await unitOfWork.BeginAsync(ct);
        Check created;
        try
        {
            created = await checkRepository.CreateAsync(check, ct);
            foreach (var alertConfigRequest in request.AlertConfigs ?? [])
                await CreateAlertConfigAsync(created, alertConfigRequest, ct);

            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }

        await scheduler.ScheduleAsync(created, ct);
        return created.ToDto();
    }

    private async Task CreateAlertConfigAsync(Check check, CreateAlertConfigRequest request, CancellationToken ct)
    {
        var config = new AlertConfig
        {
            CheckId = check.Id,
            AlertFor = request.AlertFor,
            AlertValue = request.AlertValue,
            FailureThreshold = request.FailureThreshold,
            SuccessThreshold = request.SuccessThreshold,
            Description = request.Description,
            IsActive = request.IsActive,
            Severity = request.Severity
        };
        await alertConfigRepository.CreateAsync(config, ct);
    }

    private static void EnsureAlertForAllowed(CheckType type, AlertFor alertFor)
    {
        if (!type.AllowedAlertFors().Contains(alertFor))
            throw new DomainValidationException($"{alertFor} is not a valid alert metric for a {type} check.");
    }

    /// <summary>
    /// Enforces the schedule bounds from the check manifest (RFC 0011): the interval must be at
    /// least 1 minute globally, at least the type's declared minimum, and strictly greater than the
    /// check's own timeout (so a run always finishes before its next fire). An un-derivable cron is
    /// left to the scheduler to reject.
    /// </summary>
    private void EnsureScheduleWithinBounds(CheckType type, string cron, string typeDataJson)
    {
        if (cronInterval.SmallestInterval(cron) is not { } interval)
            return; // invalid/too-rare cron — not this guard's concern

        if (interval < TimeSpan.FromMinutes(1))
            throw new DomainValidationException("Check interval must be at least 1 minute.");

        type.EnsureIntervalAllowed(interval);

        var timeout = TimeoutFromTypeData(type, typeDataJson);
        if (timeout is { } t && t >= interval)
            throw new DomainValidationException(
                $"The check timeout ({t.TotalSeconds:0}s) must be shorter than its interval ({interval.TotalSeconds:0}s).");
    }

    /// <summary>
    /// Extracts the configured timeout from a check's TypeDataJson, or null for a type that has no
    /// timeout field (DNS/SSL/GCP). Only HTTP/TCP/Ping carry a TimeoutMs.
    /// </summary>
    private static TimeSpan? TimeoutFromTypeData(CheckType type, string typeDataJson)
    {
        var ms = type switch
        {
            CheckType.HTTP => Deserialize<HttpCheckConfig>(typeDataJson)?.TimeoutMs,
            CheckType.TCP => Deserialize<TcpCheckConfig>(typeDataJson)?.TimeoutMs,
            CheckType.Ping => Deserialize<PingCheckConfig>(typeDataJson)?.TimeoutMs,
            _ => null
        };
        return ms is { } value ? TimeSpan.FromMilliseconds(value) : null;
    }

    private static readonly System.Text.Json.JsonSerializerOptions _typeDataJson =
        new() { PropertyNameCaseInsensitive = true };

    private static T? Deserialize<T>(string json)
    {
        try { return System.Text.Json.JsonSerializer.Deserialize<T>(json, _typeDataJson); }
        catch (System.Text.Json.JsonException) { return default; }
    }

    public async Task<CheckDto> UpdateAsync(string serviceSlug, string checkSlug, UpdateCheckRequest request, CancellationToken ct = default)
    {
        var service = await serviceRepository.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);
        var check = await checkRepository.GetBySlugAsync(service.Id, checkSlug, ct)
            ?? throw new NotFoundException(nameof(Check), checkSlug);

        if (request.Name is not null) check.Name = request.Name;
        if (request.Description is not null) check.Description = request.Description;
        if (request.Cron is not null) check.Cron = request.Cron;
        if (request.TypeDataJson is not null) check.TypeDataJson = request.TypeDataJson;
        if (request.IsActive is not null) check.IsActive = request.IsActive.Value;
        if (request.IsMultiRegion is not null) check.IsMultiRegion = request.IsMultiRegion.Value;
        if (request.HistoryDaysDesktop is not null) check.HistoryDaysDesktop = request.HistoryDaysDesktop;
        if (request.HistoryDaysMobile is not null) check.HistoryDaysMobile = request.HistoryDaysMobile;
        if (request.IntegrationId is not null) check.IntegrationId = request.IntegrationId;

        // Type is immutable on update; validate the resulting cron/config against the check's own type.
        EnsureScheduleWithinBounds(check.Type, check.Cron, check.TypeDataJson);

        var updated = await checkRepository.UpdateAsync(check, ct);
        await scheduler.ScheduleAsync(updated, ct);
        return updated.ToDto();
    }

    public async Task DeleteAsync(string serviceSlug, string checkSlug, CancellationToken ct = default)
    {
        var service = await serviceRepository.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);
        var check = await checkRepository.GetBySlugAsync(service.Id, checkSlug, ct)
            ?? throw new NotFoundException(nameof(Check), checkSlug);
        await checkRepository.DeleteAsync(check, ct);
        await scheduler.UnscheduleAsync(check.Id, ct);
    }

    /// <summary>Default lookback window applied to <see cref="GetRecentLogsAsync"/> when no range is given.</summary>
    private static readonly TimeSpan DefaultLogsWindow = TimeSpan.FromDays(7);

    /// <summary>
    /// Returns data points for a check in the given time range, ordered newest first.
    /// Defaults to the last 7 days when <paramref name="from"/>/<paramref name="to"/> are omitted.
    /// </summary>
    public async Task<IEnumerable<CheckDataPointDto>> GetRecentLogsAsync(
        string serviceSlug, string checkSlug, int limit = 20, string? region = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default)
    {
        var service = await serviceRepository.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);
        var check = await checkRepository.GetBySlugAsync(service.Id, checkSlug, ct)
            ?? throw new NotFoundException(nameof(Check), checkSlug);

        var toValue = to ?? DateTimeOffset.UtcNow;
        var fromValue = from ?? toValue - DefaultLogsWindow;

        var points = await dataPointRepository.GetByCheckIdAsync(
            check.Id, fromValue.ToUnixTimeSeconds(), toValue.ToUnixTimeSeconds(), region, limit, ct);
        return points.Select(p => p.ToDto());
    }

    public async Task<IEnumerable<CheckDailyStatsDto>> GetDailyStatsAsync(
        string serviceSlug, string checkSlug, int days = 14, CancellationToken ct = default)
    {
        var service = await serviceRepository.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);
        var check = await checkRepository.GetBySlugAsync(service.Id, checkSlug, ct)
            ?? throw new NotFoundException(nameof(Check), checkSlug);

        var to = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var from = to - (long)days * 86400;

        var stats = await dataPointRepository.GetDailyStatsByCheckIdAsync(check.Id, from, to, ct);
        return stats.Select(s => new CheckDailyStatsDto(s.Region, s.DayTimestamp, s.CountUp, s.CountDown, s.CountDegraded, s.AvgLatencyMs));
    }
}

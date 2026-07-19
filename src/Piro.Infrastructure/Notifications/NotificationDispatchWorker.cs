using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Notifications;

/// <summary>
/// Drains the notification outbox (RFC 0009 §4.6). A short polling loop — modeled on
/// <c>StatusDrainHostedService</c> but reading a durable table rather than an in-memory channel — that
/// processes eligible events with three guarantees:
/// <list type="bullet">
/// <item><b>Ordering per entity.</b> Within an <see cref="NotificationEventOutbox.OrderingKey"/>, a row
/// is only processed when no earlier-id row with the same key is still pending/processing. A terminal
/// (Done/Failed) earlier row does not block, so one broken destination can't freeze an entity forever.</item>
/// <item><b>Retry with backoff.</b> A throwing <see cref="INotificationEventProcessor"/> increments
/// Attempts, records the error, and reschedules NextAttemptAt exponentially, until a cap quarantines the
/// row as Failed.</item>
/// <item><b>Lease reclaim.</b> A Processing row whose lease deadline (NextAttemptAt) has passed — e.g. a
/// worker crashed mid-handle — is reclaimed and retried.</item>
/// </list>
/// The transport lives here; the response (subscription matching, dispatch, delivery-log writes) is the
/// injected <see cref="INotificationEventProcessor"/>'s job.
/// </summary>
internal class NotificationDispatchWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationDispatchWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);
    private const int MaxAttempts = 8;
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<PiroDbContext>();
                var processor = scope.ServiceProvider.GetRequiredService<INotificationEventProcessor>();

                var processed = await DrainOnceAsync(db, processor, DateTime.UtcNow, stoppingToken);
                if (processed > 0) continue; // more may be ready — drain again without waiting
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Notification dispatch drain cycle failed; will retry.");
            }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Runs a single drain cycle: reclaims expired leases, then processes up to <see cref="BatchSize"/>
    /// eligible rows in id (= emit) order, skipping any row blocked by an earlier same-key row. Returns
    /// how many rows it advanced to a terminal or rescheduled state. Public for isolated testing.
    /// </summary>
    internal async Task<int> DrainOnceAsync(
        PiroDbContext db, INotificationEventProcessor processor, DateTime now, CancellationToken ct)
    {
        // 1. Reclaim Processing rows whose lease has expired (a worker died mid-handle).
        var stuck = await db.NotificationEventOutbox
            .Where(o => o.Status == OutboxStatus.Processing && o.NextAttemptAt != null && o.NextAttemptAt <= now)
            .ToListAsync(ct);
        foreach (var row in stuck)
        {
            row.Status = OutboxStatus.Pending;
            logger.LogWarning("Reclaimed stuck notification outbox row #{Id} (lease expired).", row.Id);
        }
        if (stuck.Count > 0) await db.SaveChangesAsync(ct);

        // 2. Candidate Pending rows whose backoff has arrived, oldest first.
        var candidates = await db.NotificationEventOutbox
            .Where(o => o.Status == OutboxStatus.Pending && (o.NextAttemptAt == null || o.NextAttemptAt <= now))
            .OrderBy(o => o.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        // OrderingKeys blocked this cycle: once we skip/handle a key's lowest row, a later row of the
        // same key must wait for the next cycle so per-entity order is preserved.
        var touchedKeys = new HashSet<string>();
        var advanced = 0;

        foreach (var row in candidates)
        {
            if (ct.IsCancellationRequested) break;
            if (!touchedKeys.Add(row.OrderingKey)) continue; // an earlier row of this key already ran this cycle

            // Ordering guard: is there an earlier, non-terminal row with the same key? If so, this row
            // is not yet at the head of its entity's queue — leave it for a later cycle.
            var blocked = await db.NotificationEventOutbox.AnyAsync(o =>
                o.OrderingKey == row.OrderingKey &&
                o.Id < row.Id &&
                (o.Status == OutboxStatus.Pending || o.Status == OutboxStatus.Processing), ct);
            if (blocked) continue;

            await ProcessRowAsync(db, processor, row, now, ct);
            advanced++;
        }

        return advanced;
    }

    private async Task ProcessRowAsync(
        PiroDbContext db, INotificationEventProcessor processor, NotificationEventOutbox row,
        DateTime now, CancellationToken ct)
    {
        // Claim the row with a lease so a concurrent worker won't also take it.
        row.Status = OutboxStatus.Processing;
        row.Attempts++;
        row.NextAttemptAt = now.Add(ProcessingLease);
        await db.SaveChangesAsync(ct);

        try
        {
            await processor.ProcessAsync(row, ct);
            row.Status = OutboxStatus.Done;
            row.NextAttemptAt = null;
            row.ProcessedAt = now;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            row.LastError = ex.Message;
            if (row.Attempts >= MaxAttempts)
            {
                row.Status = OutboxStatus.Failed;
                row.NextAttemptAt = null;
                row.ProcessedAt = now;
                logger.LogError(ex,
                    "Notification outbox row #{Id} ({EventType}) failed permanently after {Attempts} attempts.",
                    row.Id, row.EventType, row.Attempts);
            }
            else
            {
                row.Status = OutboxStatus.Pending;
                row.NextAttemptAt = now.Add(BackoffFor(row.Attempts));
                logger.LogWarning(ex,
                    "Notification outbox row #{Id} ({EventType}) failed on attempt {Attempts}; retrying at {NextAttemptAt}.",
                    row.Id, row.EventType, row.Attempts, row.NextAttemptAt);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // Exponential backoff, capped: 2^attempts seconds up to ~5 minutes.
    private static TimeSpan BackoffFor(int attempts)
    {
        var seconds = Math.Min(300, Math.Pow(2, attempts));
        return TimeSpan.FromSeconds(seconds);
    }
}

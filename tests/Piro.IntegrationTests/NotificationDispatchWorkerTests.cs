using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Notifications;
using Piro.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>
/// Exercises the RFC 0009 phase-3 push-engine transport against a real Postgres instance: the drain
/// worker's ordering-per-entity, idempotency, and retry/backoff guarantees, plus the
/// NotificationEventPublisher and the UNIQUE idempotency constraint. The processor is faked so the
/// transport is tested in isolation.
/// </summary>
public class NotificationDispatchWorkerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private PiroDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var options = new DbContextOptionsBuilder<PiroDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        _db = new PiroDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _container.DisposeAsync();
    }

    private NotificationDispatchWorker NewWorker() =>
        // scopeFactory is unused by DrainOnceAsync (the path under test); the loop is not exercised here.
        new(scopeFactory: null!, NullLogger<NotificationDispatchWorker>.Instance);

    private async Task<NotificationEventOutbox> EnqueueAsync(string eventType, string orderingKey)
    {
        var row = new NotificationEventOutbox
        {
            EventType = eventType,
            OrderingKey = orderingKey,
            PayloadJson = "{}",
            Status = OutboxStatus.Pending,
            // CreatedAt stamped by the audit hook on save.
        };
        _db.NotificationEventOutbox.Add(row);
        await _db.SaveChangesAsync();
        return row;
    }

    // A processor that records the order it saw events, and can be told to throw.
    private sealed class RecordingProcessor : INotificationEventProcessor
    {
        public List<long> Processed { get; } = [];
        public Func<NotificationEventOutbox, bool>? ThrowWhen { get; set; }

        public Task ProcessAsync(NotificationEventOutbox row, CancellationToken ct = default)
        {
            if (ThrowWhen?.Invoke(row) == true)
                throw new InvalidOperationException("boom");
            Processed.Add(row.Id);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DrainOnce_ProcessesPendingRow_AndMarksItDone()
    {
        var row = await EnqueueAsync("alert:created", "alert:1");
        var processor = new RecordingProcessor();

        var advanced = await NewWorker().DrainOnceAsync(_db, processor, DateTime.UtcNow, default);

        advanced.Should().Be(1);
        processor.Processed.Should().ContainSingle().Which.Should().Be(row.Id);
        var reloaded = await _db.NotificationEventOutbox.FindAsync(row.Id);
        reloaded!.Status.Should().Be(OutboxStatus.Done);
        reloaded.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DrainOnce_ProcessesOnlyTheHeadOfEachOrderingKey_InOneCycle()
    {
        // Two events of the same entity: only the earlier one may run this cycle.
        var first = await EnqueueAsync("alert:created", "alert:7");
        var second = await EnqueueAsync("alert:resolved", "alert:7");
        var processor = new RecordingProcessor();

        var advanced = await NewWorker().DrainOnceAsync(_db, processor, DateTime.UtcNow, default);

        advanced.Should().Be(1);
        processor.Processed.Should().Equal(first.Id);

        // Next cycle: first is Done, so second becomes the head and runs.
        var advanced2 = await NewWorker().DrainOnceAsync(_db, processor, DateTime.UtcNow, default);
        advanced2.Should().Be(1);
        processor.Processed.Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task DrainOnce_DifferentOrderingKeys_AllProcessInOneCycle()
    {
        var a = await EnqueueAsync("alert:created", "alert:1");
        var b = await EnqueueAsync("alert:created", "alert:2");
        var c = await EnqueueAsync("incident:created", "incident:9");
        var processor = new RecordingProcessor();

        var advanced = await NewWorker().DrainOnceAsync(_db, processor, DateTime.UtcNow, default);

        advanced.Should().Be(3);
        processor.Processed.Should().BeEquivalentTo([a.Id, b.Id, c.Id]);
    }

    [Fact]
    public async Task DrainOnce_ProcessorThrows_ReschedulesWithBackoff_AndKeepsRowPending()
    {
        var row = await EnqueueAsync("alert:created", "alert:5");
        var now = DateTime.UtcNow;
        var processor = new RecordingProcessor { ThrowWhen = _ => true };

        await NewWorker().DrainOnceAsync(_db, processor, now, default);

        var reloaded = await _db.NotificationEventOutbox.FindAsync(row.Id);
        reloaded!.Status.Should().Be(OutboxStatus.Pending);
        reloaded.Attempts.Should().Be(1);
        reloaded.LastError.Should().Be("boom");
        reloaded.NextAttemptAt.Should().BeAfter(now, "backoff should push the next attempt into the future");
    }

    [Fact]
    public async Task DrainOnce_ProcessorKeepsThrowing_QuarantinesAsFailed_AfterMaxAttempts()
    {
        var row = await EnqueueAsync("alert:created", "alert:6");
        var processor = new RecordingProcessor { ThrowWhen = _ => true };
        var worker = NewWorker();

        // Drive attempts until the row is quarantined; each cycle uses a 'now' past the backoff.
        var now = DateTime.UtcNow;
        for (var i = 0; i < 10; i++)
        {
            now = now.AddMinutes(10);
            await worker.DrainOnceAsync(_db, processor, now, default);
            var state = await _db.NotificationEventOutbox.AsNoTracking().FirstAsync(o => o.Id == row.Id);
            if (state.Status == OutboxStatus.Failed) break;
        }

        var reloaded = await _db.NotificationEventOutbox.AsNoTracking().FirstAsync(o => o.Id == row.Id);
        reloaded.Status.Should().Be(OutboxStatus.Failed);
        reloaded.LastError.Should().Be("boom");
        reloaded.ProcessedAt.Should().NotBeNull();
        reloaded.Attempts.Should().BeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public async Task DrainOnce_ReclaimsExpiredProcessingLease()
    {
        var now = DateTime.UtcNow;
        // A row left Processing with a lease deadline in the past — as if a worker crashed mid-handle.
        var row = await EnqueueAsync("alert:created", "alert:8");
        row.Status = OutboxStatus.Processing;
        row.NextAttemptAt = now.AddMinutes(-1);
        await _db.SaveChangesAsync();

        var processor = new RecordingProcessor();
        var advanced = await NewWorker().DrainOnceAsync(_db, processor, now, default);

        advanced.Should().Be(1);
        var reloaded = await _db.NotificationEventOutbox.FindAsync(row.Id);
        reloaded!.Status.Should().Be(OutboxStatus.Done);
    }

    [Fact]
    public async Task Publisher_WritesPendingRow_WithOrderingKeyAndPayload()
    {
        var publisher = new NotificationEventPublisher(_db);
        var evt = new Piro.Application.Models.NotificationEvents.AlertResolvedPayloadV1(
            AlertId: 42, ServiceName: "api", CheckName: "http", Severity: AlertSeverity.Critical,
            Tags: [], ResolvedAt: DateTimeOffset.UtcNow);

        var id = await publisher.PublishAsync(evt, "alert:42");

        var row = await _db.NotificationEventOutbox.AsNoTracking().FirstAsync(o => o.Id == id);
        row.Status.Should().Be(OutboxStatus.Pending);
        row.EventType.Should().Be("alert:resolved");
        row.OrderingKey.Should().Be("alert:42");
        row.PayloadJson.Should().Contain("\"AlertId\":42");
    }

    [Fact]
    public async Task DeliveryLog_IdempotencyKey_IsUnique()
    {
        _db.NotificationDeliveryLogs.Add(new NotificationDeliveryLog
        {
            IdempotencyKey = "alert:created:1:sub-a", EventType = "alert:created",
            TargetKind = "Group", TargetDescriptor = "Slack #ops",
            Status = DeliveryStatus.Delivered, AttemptedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        _db.NotificationDeliveryLogs.Add(new NotificationDeliveryLog
        {
            IdempotencyKey = "alert:created:1:sub-a", EventType = "alert:created",
            TargetKind = "Group", TargetDescriptor = "Slack #ops",
            Status = DeliveryStatus.Delivered, AttemptedAt = DateTime.UtcNow,
        });

        var act = async () => await _db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("the UNIQUE idempotency index blocks a duplicate delivery");
    }
}

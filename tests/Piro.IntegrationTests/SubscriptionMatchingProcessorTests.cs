using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Models.NotificationEvents;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Notifications;
using Piro.Infrastructure.Persistence;
using Piro.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>
/// Exercises the RFC 0009 phase-4 subscription matching + personal delivery against real Postgres:
/// severity gating, delivery to a Personal destination via a fake dispatcher, DeliveryLog recording,
/// and effectively-once idempotency on replay.
/// </summary>
public class SubscriptionMatchingProcessorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private PiroDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var options = new DbContextOptionsBuilder<PiroDbContext>().UseNpgsql(_container.GetConnectionString()).Options;
        _db = new PiroDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _container.DisposeAsync();
    }

    // Records every (handle, content) it was asked to deliver.
    private sealed class FakeEmailDispatcher : IPersonalNotificationDispatcher<AlertNotificationContext>
    {
        public List<(string handle, AlertNotificationContext content)> Sent { get; } = [];
        public IntegrationType Type => IntegrationType.Email;
        public Task<bool> SendAsync(Integration? i, string handle, AlertNotificationContext c, CancellationToken ct = default)
        {
            Sent.Add((handle, c));
            return Task.FromResult(true);
        }
    }

    private SubscriptionMatchingProcessor NewProcessor(FakeEmailDispatcher dispatcher) =>
        new(
            new NotificationSubscriptionRepository(_db),
            new UserNotificationPreferenceRepository(_db),
            new NotificationDeliveryLogRepository(_db),
            [dispatcher],
            NullLogger<SubscriptionMatchingProcessor>.Instance);

    private async Task<AppUser> SeedUserWithVerifiedEmailPrefAsync(string email)
    {
        var user = new AppUser { UserName = email, Email = email, CreatedAt = DateTime.UtcNow };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            UserId = user.Id,
            Channel = PersonalNotificationChannel.Email,
            Handle = email,
            Priority = 0,
            VerifiedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<NotificationSubscription> SeedPersonalSubscriptionAsync(
        int userId, AlertSeverity minSeverity, params string[] events)
    {
        var sub = new NotificationSubscription
        {
            Name = "test sub",
            EventsJson = JsonSerializer.Serialize(events),
            MinSeverity = minSeverity,
            TargetKind = NotificationTargetKind.Personal,
            UserId = userId,
            Enabled = true,
        };
        _db.NotificationSubscriptions.Add(sub);
        await _db.SaveChangesAsync();
        return sub;
    }

    private static NotificationEventOutbox AlertCreatedRow(int alertId, AlertSeverity severity)
    {
        var payload = new AlertCreatedPayloadV1(alertId, "api", "http", severity, [], false, null, DateTimeOffset.UtcNow);
        return new NotificationEventOutbox
        {
            Id = alertId, // in tests, use alertId as the row id for a stable idempotency key
            EventType = "alert:created",
            OrderingKey = $"alert:{alertId}",
            PayloadJson = JsonSerializer.Serialize(payload),
            Status = OutboxStatus.Processing,
        };
    }

    [Fact]
    public async Task Matches_AndDelivers_ToPersonalDestination()
    {
        var user = await SeedUserWithVerifiedEmailPrefAsync("jane@example.com");
        await SeedPersonalSubscriptionAsync(user.Id, AlertSeverity.Warning, "alert:created");
        var dispatcher = new FakeEmailDispatcher();

        await NewProcessor(dispatcher).ProcessAsync(AlertCreatedRow(alertId: 100, AlertSeverity.Critical), default);

        dispatcher.Sent.Should().ContainSingle();
        dispatcher.Sent[0].handle.Should().Be("jane@example.com");

        var log = await _db.NotificationDeliveryLogs.AsNoTracking().SingleAsync();
        log.Status.Should().Be(DeliveryStatus.Delivered);
        var subId = (await _db.NotificationSubscriptions.AsNoTracking().FirstAsync()).Id;
        log.IdempotencyKey.Should().Be($"alert:created:100:{subId}");
    }

    [Fact]
    public async Task DoesNotDeliver_WhenSeverityBelowMinimum()
    {
        var user = await SeedUserWithVerifiedEmailPrefAsync("jane2@example.com");
        await SeedPersonalSubscriptionAsync(user.Id, AlertSeverity.Critical, "alert:created");
        var dispatcher = new FakeEmailDispatcher();

        await NewProcessor(dispatcher).ProcessAsync(AlertCreatedRow(alertId: 101, AlertSeverity.Warning), default);

        dispatcher.Sent.Should().BeEmpty();
        (await _db.NotificationDeliveryLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DoesNotDeliver_WhenEventNotSubscribed()
    {
        var user = await SeedUserWithVerifiedEmailPrefAsync("jane3@example.com");
        await SeedPersonalSubscriptionAsync(user.Id, AlertSeverity.Warning, "alert:resolved"); // not created
        var dispatcher = new FakeEmailDispatcher();

        await NewProcessor(dispatcher).ProcessAsync(AlertCreatedRow(alertId: 102, AlertSeverity.Critical), default);

        dispatcher.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Replay_IsIdempotent_DoesNotDeliverTwice()
    {
        var user = await SeedUserWithVerifiedEmailPrefAsync("jane4@example.com");
        await SeedPersonalSubscriptionAsync(user.Id, AlertSeverity.Warning, "alert:created");
        var dispatcher = new FakeEmailDispatcher();
        var row = AlertCreatedRow(alertId: 103, AlertSeverity.Critical);

        await NewProcessor(dispatcher).ProcessAsync(row, default);
        await NewProcessor(dispatcher).ProcessAsync(row, default); // replay same event

        dispatcher.Sent.Should().ContainSingle("the idempotency key blocks a second delivery");
        (await _db.NotificationDeliveryLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GroupDestination_IsLoggedAsSkipped_UntilPhase5()
    {
        // A group subscription pointing at an integration; group delivery isn't implemented yet.
        var integration = new Integration { Id = Guid.NewGuid(), Type = IntegrationType.Slack, Name = "Ops Slack" };
        _db.Integrations.Add(integration);
        await _db.SaveChangesAsync();

        _db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            Name = "slack", EventsJson = JsonSerializer.Serialize(new[] { "alert:created" }),
            MinSeverity = AlertSeverity.Warning, TargetKind = NotificationTargetKind.Group,
            IntegrationId = integration.Id, Enabled = true,
        });
        await _db.SaveChangesAsync();
        var dispatcher = new FakeEmailDispatcher();

        await NewProcessor(dispatcher).ProcessAsync(AlertCreatedRow(alertId: 104, AlertSeverity.Critical), default);

        dispatcher.Sent.Should().BeEmpty();
        var log = await _db.NotificationDeliveryLogs.AsNoTracking().SingleAsync();
        log.Status.Should().Be(DeliveryStatus.Skipped);
        log.TargetKind.Should().Be("Group");
    }
}

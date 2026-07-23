using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Piro.Application.Models.NotificationEvents;
using Piro.Contracts;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Notifications;
using Piro.Infrastructure.Persistence;
using Piro.Infrastructure.Persistence.Repositories;
using Piro.Integrations.Abstractions;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>
/// Exercises the RFC 0009/0016 subscription matching + delivery against real Postgres: severity
/// gating, event membership, delivery to a Personal or Channel destination via a fake
/// <see cref="IIntegrationEventHandler"/>, DeliveryLog recording, and effectively-once idempotency
/// on replay.
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

    // Records every (target, event) it was asked to handle, for one integration id.
    private sealed class FakeEventHandler(string integrationId) : IIntegrationEventHandler
    {
        public List<(string? target, Event evt)> Handled { get; } = [];
        public string IntegrationId => integrationId;
        public Task<bool> HandleAsync(Event evt, EventDeliveryContext ctx, IIntegrationHost host, CancellationToken ct = default)
        {
            Handled.Add((ctx.Target, evt));
            return Task.FromResult(true);
        }
    }

    // A no-op host — the fake handler never asks it for anything.
    private sealed class FakeHost : IIntegrationHost
    {
        public T GetRequiredService<T>() where T : notnull => throw new NotSupportedException();
        public Task<TConfig?> GetConfigAsync<TConfig>(Guid integrationId, CancellationToken ct = default) where TConfig : class =>
            Task.FromResult<TConfig?>(null);
    }

    private SubscriptionMatchingProcessor NewProcessor(params IIntegrationEventHandler[] handlers) =>
        new(
            new NotificationSubscriptionRepository(_db),
            new UserNotificationPreferenceRepository(_db),
            new NotificationDeliveryLogRepository(_db),
            handlers,
            new FakeHost(),
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

    private static NotificationEventOutbox AlertCreatedRow(int alertId, AlertSeverity severity, int? serviceId = null)
    {
        var payload = new AlertCreatedPayload(alertId, "api", "http", severity, [], false, null, DateTimeOffset.UtcNow, ServiceId: serviceId);
        return new NotificationEventOutbox
        {
            Id = alertId,
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
        var handler = new FakeEventHandler("Email");

        await NewProcessor(handler).ProcessAsync(AlertCreatedRow(alertId: 100, AlertSeverity.Critical), default);

        handler.Handled.Should().ContainSingle();
        handler.Handled[0].target.Should().Be("jane@example.com");

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
        var handler = new FakeEventHandler("Email");

        await NewProcessor(handler).ProcessAsync(AlertCreatedRow(alertId: 101, AlertSeverity.Warning), default);

        handler.Handled.Should().BeEmpty();
        (await _db.NotificationDeliveryLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DoesNotDeliver_WhenEventNotSubscribed()
    {
        var user = await SeedUserWithVerifiedEmailPrefAsync("jane3@example.com");
        await SeedPersonalSubscriptionAsync(user.Id, AlertSeverity.Warning, "alert:resolved");
        var handler = new FakeEventHandler("Email");

        await NewProcessor(handler).ProcessAsync(AlertCreatedRow(alertId: 102, AlertSeverity.Critical), default);

        handler.Handled.Should().BeEmpty();
    }

    [Fact]
    public async Task Replay_IsIdempotent_DoesNotDeliverTwice()
    {
        var user = await SeedUserWithVerifiedEmailPrefAsync("jane4@example.com");
        await SeedPersonalSubscriptionAsync(user.Id, AlertSeverity.Warning, "alert:created");
        var handler = new FakeEventHandler("Email");
        var row = AlertCreatedRow(alertId: 103, AlertSeverity.Critical);

        await NewProcessor(handler).ProcessAsync(row, default);
        await NewProcessor(handler).ProcessAsync(row, default);

        handler.Handled.Should().ContainSingle("the idempotency key blocks a second delivery");
        (await _db.NotificationDeliveryLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ChannelDestination_DeliversViaIntegrationHandler()
    {
        var integration = new Integration { Id = Guid.NewGuid(), Type = "GoogleChat", Name = "Ops Space" };
        _db.Integrations.Add(integration);
        _db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            Name = "gchat", EventsJson = JsonSerializer.Serialize(new[] { "alert:created" }),
            MinSeverity = AlertSeverity.Warning, TargetKind = NotificationTargetKind.Channel,
            IntegrationId = integration.Id, Enabled = true,
        });
        await _db.SaveChangesAsync();
        var handler = new FakeEventHandler("GoogleChat");

        await NewProcessor(handler).ProcessAsync(AlertCreatedRow(alertId: 200, AlertSeverity.Critical), default);

        handler.Handled.Should().ContainSingle();
        var log = await _db.NotificationDeliveryLogs.AsNoTracking().SingleAsync();
        log.Status.Should().Be(DeliveryStatus.Delivered);
        log.TargetKind.Should().Be("Channel");
    }
}

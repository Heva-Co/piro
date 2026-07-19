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

    private SubscriptionMatchingProcessor NewProcessor(
        FakeEmailDispatcher dispatcher,
        IEnumerable<IChannelNotificationDispatcher<AlertNotificationContext>>? group = null,
        IEnumerable<ISystemEventDispatcher>? systemEvent = null) =>
        new(
            new NotificationSubscriptionRepository(_db),
            new UserNotificationPreferenceRepository(_db),
            new NotificationDeliveryLogRepository(_db),
            new ServiceIntegrationMappingRepository(_db),
            [dispatcher],
            group ?? [],
            systemEvent ?? [],
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

    private sealed class FakeChannelDispatcher(IntegrationType type) : IChannelNotificationDispatcher<AlertNotificationContext>
    {
        public List<string?> Posted { get; } = [];
        public IntegrationType Type => type;
        public Task<bool> SendAsync(Integration integration, string? target, AlertNotificationContext c, CancellationToken ct = default)
        {
            Posted.Add(target);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeSystemEventDispatcher(IntegrationType type) : ISystemEventDispatcher
    {
        public List<(string routingKey, string dedupKey, string action)> Events { get; } = [];
        public IntegrationType Type => type;
        public Task<bool> TriggerAsync(string routingKey, string dedupKey, AlertNotificationContext context, CancellationToken ct = default)
        { Events.Add((routingKey, dedupKey, "trigger")); return Task.FromResult(true); }
        public Task<bool> ResolveAsync(string routingKey, string dedupKey, CancellationToken ct = default)
        { Events.Add((routingKey, dedupKey, "resolve")); return Task.FromResult(true); }
    }

    [Fact]
    public async Task GroupDestination_DeliversViaGroupDispatcher()
    {
        var integration = new Integration { Id = Guid.NewGuid(), Type = IntegrationType.GoogleChat, Name = "Ops Space" };
        _db.Integrations.Add(integration);
        _db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            Name = "gchat", EventsJson = JsonSerializer.Serialize(new[] { "alert:created" }),
            MinSeverity = AlertSeverity.Warning, TargetKind = NotificationTargetKind.Channel,
            IntegrationId = integration.Id, Enabled = true,
        });
        await _db.SaveChangesAsync();
        var group = new FakeChannelDispatcher(IntegrationType.GoogleChat);

        await NewProcessor(new FakeEmailDispatcher(), group: [group])
            .ProcessAsync(AlertCreatedRow(alertId: 200, AlertSeverity.Critical), default);

        group.Posted.Should().ContainSingle();
        var log = await _db.NotificationDeliveryLogs.AsNoTracking().SingleAsync();
        log.Status.Should().Be(DeliveryStatus.Delivered);
        log.TargetKind.Should().Be("Channel");
    }

    [Fact]
    public async Task IntegrationDestination_ResolvesRoutingKeyFromMapping_AndTriggers()
    {
        // A service, a PagerDuty integration, and their mapping carrying the routing key (RFC 0004).
        var service = new Service { Name = "API", Slug = "api" };
        _db.Services.Add(service);
        var integration = new Integration { Id = Guid.NewGuid(), Type = IntegrationType.PagerDuty, Name = "PD" };
        _db.Integrations.Add(integration);
        await _db.SaveChangesAsync();
        _db.ServiceIntegrationMappings.Add(new ServiceIntegrationMapping
        {
            ServiceId = service.Id, IntegrationId = integration.Id,
            MappingJson = "{\"pagerDutyServiceId\":\"PDSVC\",\"routingKey\":\"R0ROUTINGKEY\"}",
        });
        _db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            Name = "pd", EventsJson = JsonSerializer.Serialize(new[] { "alert:created" }),
            MinSeverity = AlertSeverity.Warning, TargetKind = NotificationTargetKind.Integration,
            IntegrationId = integration.Id, Enabled = true,
        });
        await _db.SaveChangesAsync();

        var pd = new FakeSystemEventDispatcher(IntegrationType.PagerDuty);
        var row = AlertCreatedRow(alertId: 201, AlertSeverity.Critical, serviceId: service.Id);

        await NewProcessor(new FakeEmailDispatcher(), systemEvent: [pd]).ProcessAsync(row, default);

        pd.Events.Should().ContainSingle();
        pd.Events[0].routingKey.Should().Be("R0ROUTINGKEY");
        pd.Events[0].action.Should().Be("trigger");
        (await _db.NotificationDeliveryLogs.AsNoTracking().SingleAsync()).Status.Should().Be(DeliveryStatus.Delivered);
    }

    [Fact]
    public async Task IntegrationDestination_NoMapping_IsSkipped()
    {
        var service = new Service { Name = "API2", Slug = "api2" };
        _db.Services.Add(service);
        var integration = new Integration { Id = Guid.NewGuid(), Type = IntegrationType.PagerDuty, Name = "PD2" };
        _db.Integrations.Add(integration);
        _db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            Name = "pd2", EventsJson = JsonSerializer.Serialize(new[] { "alert:created" }),
            MinSeverity = AlertSeverity.Warning, TargetKind = NotificationTargetKind.Integration,
            IntegrationId = integration.Id, Enabled = true,
        });
        await _db.SaveChangesAsync();

        var pd = new FakeSystemEventDispatcher(IntegrationType.PagerDuty);
        var row = AlertCreatedRow(alertId: 202, AlertSeverity.Critical, serviceId: service.Id);

        await NewProcessor(new FakeEmailDispatcher(), systemEvent: [pd]).ProcessAsync(row, default);

        pd.Events.Should().BeEmpty("no mapping means no routing key");
        (await _db.NotificationDeliveryLogs.AsNoTracking().SingleAsync()).Status.Should().Be(DeliveryStatus.Skipped);
    }
}

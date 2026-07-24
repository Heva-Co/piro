using FluentAssertions;
using NSubstitute;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;
using Piro.Integrations.Abstractions;

namespace Piro.UnitTests;

/// <summary>
/// The #212 guard (RFC 0016 §4.5): a subscription that targets an integration is rejected unless the
/// integration declares <see cref="IntegrationCapability.SubscribesToEvents"/> and handles every chosen
/// event. Before this, an admin could subscribe an integration to events it never emits, and the
/// mismatch surfaced only as silently-dropped notifications at delivery time.
/// </summary>
public class NotificationSubscriptionEventGuardTests
{
    private static readonly Guid IntegrationId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly INotificationSubscriptionRepository _repo = Substitute.For<INotificationSubscriptionRepository>();
    private readonly IIntegrationRepository _integrationRepo = Substitute.For<IIntegrationRepository>();
    private readonly IIntegrationRegistry _registry = new GuardTestRegistry();
    private readonly NotificationSubscriptionAppService _sut;

    public NotificationSubscriptionEventGuardTests()
    {
        _sut = new NotificationSubscriptionAppService(_repo, _integrationRepo, _registry);
        _repo.CreateAsync(Arg.Any<NotificationSubscription>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<NotificationSubscription>());
    }

    private void StubIntegration(string type)
    {
        _integrationRepo.GetByIdAsync(IntegrationId, Arg.Any<CancellationToken>())
            .Returns(new Integration { Id = IntegrationId, Name = "Test", Type = type });
    }

    private static UpsertNotificationSubscriptionRequest Request(params string[] events) =>
        new("Sub", events, AlertSeverity.Warning, NotificationTargetKind.Channel,
            UserId: null, IntegrationId: IntegrationId, Target: null, Enabled: true);

    [Fact]
    public async Task Create_WithSupportedEvent_Succeeds()
    {
        StubIntegration("Telegram");

        var act = () => _sut.CreateAsync(Request("alert:created"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Create_WithEventTheIntegrationDoesNotHandle_Throws()
    {
        StubIntegration("Telegram");

        var act = () => _sut.CreateAsync(Request("incident:created"));

        (await act.Should().ThrowAsync<DomainValidationException>())
            .Which.Message.Should().Contain("incident:created");
    }

    [Fact]
    public async Task Create_ForIntegrationThatDoesNotSubscribe_Throws()
    {
        StubIntegration("NotSubscribing");

        var act = () => _sut.CreateAsync(Request("alert:created"));

        (await act.Should().ThrowAsync<DomainValidationException>())
            .Which.Message.Should().Contain("does not support event subscriptions");
    }

    [Fact]
    public async Task Create_ForUnknownIntegrationType_Throws()
    {
        StubIntegration("Legacy");

        var act = () => _sut.CreateAsync(Request("alert:created"));

        (await act.Should().ThrowAsync<DomainValidationException>())
            .Which.Message.Should().Contain("not a known integration");
    }
}

file sealed class GuardTestStub(string id, IntegrationCapability caps, string[] events) : IIntegration
{
    public string IntegrationId => id;
    public IntegrationManifest Manifest => new()
    {
        Capabilities = caps,
        ConfigType = typeof(object),
        SupportedEvents = events,
    };
}

file sealed class GuardTestRegistry : IIntegrationRegistry
{
    private readonly Dictionary<string, IIntegration> _byId = new(StringComparer.Ordinal)
    {
        ["Telegram"] = new GuardTestStub("Telegram",
            IntegrationCapability.SubscribesToEvents,
            ["alert:created", "alert:acknowledged", "alert:resolved"]),
        ["NotSubscribing"] = new GuardTestStub("NotSubscribing",
            IntegrationCapability.SendsPersonalNotification, []),
    };

    public IReadOnlyList<IIntegration> All => _byId.Values.ToList();
    public IIntegration? Find(string integrationId) => _byId.GetValueOrDefault(integrationId);
}

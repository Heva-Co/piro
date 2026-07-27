using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Piro.Contracts;
using Piro.Integrations.Abstractions;
using Piro.Integrations.MobilePush;
using Piro.Integrations.MobilePush.Crypto;
using Piro.Integrations.MobilePush.Transport;

namespace Piro.UnitTests;

/// <summary>
/// The core promise of MobilePush (RFC 0008 mobile app): a single personal notification fans out to
/// <em>every</em> device the user has, on both platforms at once, and dead tokens are pruned. These tests
/// exercise the dispatcher's orchestration against fake transports and a fake device reader.
/// </summary>
public class MobilePushDispatcherTests
{
    private static readonly EventDeliveryContext PersonalToUser42 = new()
    {
        Mode = EventDeliveryMode.Personal,
        Target = "42",
        IntegrationInstanceId = Guid.NewGuid(),
    };

    private static AlertCreatedEvent CriticalAlert() => new()
    {
        Severity = EventSeverity.Critical,
        Title = "API is down",
        ServiceName = "API",
        CheckName = "health",
        CurrentStatus = "Down",
    };

    [Fact]
    public async Task FansOutToEveryDeviceOnBothPlatforms()
    {
        var android = new RecordingTransport(DevicePushPlatform.Android, PushSendResult.Sent);
        var ios = new RecordingTransport(DevicePushPlatform.Ios, PushSendResult.Sent);
        var reader = ReaderWith(
            new DeviceTokenInfo(DevicePushPlatform.Android, "a1"),
            new DeviceTokenInfo(DevicePushPlatform.Android, "a2"),
            new DeviceTokenInfo(DevicePushPlatform.Ios, "i1"));
        var host = HostWith(reader);
        var dispatcher = new MobilePushNotificationDispatcher([android, ios], new PushPayloadSealer(), NullLogger<MobilePushNotificationDispatcher>.Instance);

        var delivered = await dispatcher.HandleAsync(CriticalAlert(), PersonalToUser42, host);

        delivered.Should().BeTrue();
        android.SentTokens.Should().BeEquivalentTo("a1", "a2");
        ios.SentTokens.Should().BeEquivalentTo("i1");
        await reader.DidNotReceive().PruneTokensAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CriticalAlert_IsSentAsCritical_ToBypassDnd()
    {
        var android = new RecordingTransport(DevicePushPlatform.Android, PushSendResult.Sent);
        var dispatcher = new MobilePushNotificationDispatcher([android], new PushPayloadSealer(), NullLogger<MobilePushNotificationDispatcher>.Instance);

        await dispatcher.HandleAsync(CriticalAlert(), PersonalToUser42,
            HostWith(ReaderWith(new DeviceTokenInfo(DevicePushPlatform.Android, "a1"))));

        android.LastMessage!.Critical.Should().BeTrue();
    }

    [Fact]
    public async Task ResolvedAlert_IsNotCritical()
    {
        var android = new RecordingTransport(DevicePushPlatform.Android, PushSendResult.Sent);
        var dispatcher = new MobilePushNotificationDispatcher([android], new PushPayloadSealer(), NullLogger<MobilePushNotificationDispatcher>.Instance);
        var recovery = new AlertResolvedEvent
        {
            Severity = EventSeverity.Critical, Title = "API recovered", ServiceName = "API", CheckName = "health",
        };

        await dispatcher.HandleAsync(recovery, PersonalToUser42,
            HostWith(ReaderWith(new DeviceTokenInfo(DevicePushPlatform.Android, "a1"))));

        android.LastMessage!.Critical.Should().BeFalse();
    }

    [Fact]
    public async Task PrunesUnregisteredTokens_ButStillDeliversToLiveOnes()
    {
        var android = new RecordingTransport(DevicePushPlatform.Android, token => token == "dead"
            ? PushSendResult.Unregistered
            : PushSendResult.Sent);
        var reader = ReaderWith(
            new DeviceTokenInfo(DevicePushPlatform.Android, "live"),
            new DeviceTokenInfo(DevicePushPlatform.Android, "dead"));
        var dispatcher = new MobilePushNotificationDispatcher([android], new PushPayloadSealer(), NullLogger<MobilePushNotificationDispatcher>.Instance);

        var delivered = await dispatcher.HandleAsync(CriticalAlert(), PersonalToUser42, HostWith(reader));

        delivered.Should().BeTrue();
        await reader.Received(1).PruneTokensAsync(
            Arg.Is<IEnumerable<string>>(t => t.Single() == "dead"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsFalse_WhenUserHasNoDevices()
    {
        var dispatcher = new MobilePushNotificationDispatcher([new RecordingTransport(DevicePushPlatform.Android, PushSendResult.Sent)], new PushPayloadSealer(),
            NullLogger<MobilePushNotificationDispatcher>.Instance);

        var delivered = await dispatcher.HandleAsync(CriticalAlert(), PersonalToUser42, HostWith(ReaderWith()));

        delivered.Should().BeFalse();
    }

    [Fact]
    public async Task ReturnsFalse_ForNonPersonalOrUnparseableTarget()
    {
        var dispatcher = new MobilePushNotificationDispatcher([new RecordingTransport(DevicePushPlatform.Android, PushSendResult.Sent)], new PushPayloadSealer(),
            NullLogger<MobilePushNotificationDispatcher>.Instance);
        var channelCtx = new EventDeliveryContext { Mode = EventDeliveryMode.Channel, Target = "42" };

        (await dispatcher.HandleAsync(CriticalAlert(), channelCtx, HostWith(ReaderWith()))).Should().BeFalse();
    }

    private static IDeviceTokenReader ReaderWith(params DeviceTokenInfo[] devices)
    {
        var reader = Substitute.For<IDeviceTokenReader>();
        reader.GetByUserIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<DeviceTokenInfo>)devices);
        return reader;
    }

    private static IIntegrationHost HostWith(IDeviceTokenReader reader)
    {
        var host = Substitute.For<IIntegrationHost>();
        host.GetRequiredService<IDeviceTokenReader>().Returns(reader);
        host.GetConfigAsync<MobilePushConfig>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new MobilePushConfig());
        return host;
    }

    private sealed class RecordingTransport(DevicePushPlatform platform, Func<string, PushSendResult> resultFor) : IPushTransport
    {
        public RecordingTransport(DevicePushPlatform platform, PushSendResult fixedResult)
            : this(platform, _ => fixedResult) { }

        public DevicePushPlatform Platform => platform;
        public PushTransportMode Mode { get; init; } = PushTransportMode.Direct;
        public List<string> SentTokens { get; } = [];
        public PushMessage? LastMessage { get; private set; }

        public bool IsConfigured(MobilePushConfig config) => true;

        public Task<PushSendResult> SendAsync(string token, PushMessage message, MobilePushConfig config, CancellationToken ct = default)
        {
            LastMessage = message;
            var result = resultFor(token);
            if (result == PushSendResult.Sent) SentTokens.Add(token);
            return Task.FromResult(result);
        }
    }
}

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Piro.Application.Models;
using Piro.Domain.Enums;
using Piro.Infrastructure.Alerts;

namespace Piro.UnitTests;

/// <summary>
/// Verifies the PagerDuty Events API v2 dispatcher (RFC 0004 Phase 3): payload shape, severity mapping,
/// dedup-key pairing of trigger↔resolve, and clean failure handling (never throws on a bad key).
/// </summary>
public class PagerDutyDispatcherTests
{
    private static AlertNotificationContext Context(AlertSeverity severity = AlertSeverity.Critical) => new(
        ServiceName: "Checkout",
        CheckName: "HTTP /health",
        CurrentStatus: ServiceStatus.DOWN,
        AlertDescription: "health check failing",
        Severity: severity,
        IsRecovery: false,
        FiredAt: DateTimeOffset.Parse("2026-07-18T15:00:00Z"),
        AlertId: 42,
        AlertUrl: "https://piro.example/admin/alerts/42");

    private static (PagerDutyDispatcher Dispatcher, CapturingHandler Handler) Build(HttpStatusCode status = HttpStatusCode.Accepted, string? body = null)
    {
        var handler = new CapturingHandler(status, body);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("piro-webhook").Returns(_ => new HttpClient(handler));
        return (new PagerDutyDispatcher(factory, NullLogger<PagerDutyDispatcher>.Instance), handler);
    }

    [Fact]
    public async Task Trigger_SendsWellFormedEventsV2Payload()
    {
        var (dispatcher, handler) = Build();

        var ok = await dispatcher.TriggerAsync("RK123", "piro-alert-42", Context());

        ok.Should().BeTrue();
        handler.LastRequestUri!.ToString().Should().Be("https://events.pagerduty.com/v2/enqueue");
        var json = JsonDocument.Parse(handler.LastBody!).RootElement;
        json.GetProperty("routing_key").GetString().Should().Be("RK123");
        json.GetProperty("event_action").GetString().Should().Be("trigger");
        json.GetProperty("dedup_key").GetString().Should().Be("piro-alert-42");
        var payload = json.GetProperty("payload");
        payload.GetProperty("summary").GetString().Should().Be("HTTP /health on Checkout");
        payload.GetProperty("source").GetString().Should().Be("Checkout");
        payload.GetProperty("severity").GetString().Should().Be("critical");
    }

    [Theory]
    [InlineData(AlertSeverity.Critical, "critical")]
    [InlineData(AlertSeverity.Warning, "warning")]
    public async Task Trigger_MapsSeverity(AlertSeverity severity, string expected)
    {
        var (dispatcher, handler) = Build();

        await dispatcher.TriggerAsync("RK", "dk", Context(severity));

        var json = JsonDocument.Parse(handler.LastBody!).RootElement;
        json.GetProperty("payload").GetProperty("severity").GetString().Should().Be(expected);
    }

    [Fact]
    public async Task Resolve_SendsResolveActionWithSameDedupKeyAndNoPayload()
    {
        var (dispatcher, handler) = Build();

        var ok = await dispatcher.ResolveAsync("RK123", "piro-alert-42");

        ok.Should().BeTrue();
        var json = JsonDocument.Parse(handler.LastBody!).RootElement;
        json.GetProperty("event_action").GetString().Should().Be("resolve");
        json.GetProperty("dedup_key").GetString().Should().Be("piro-alert-42");
        json.TryGetProperty("payload", out _).Should().BeFalse("resolve references the alert by dedup_key only");
    }

    [Fact]
    public async Task Trigger_BadRoutingKey_ReturnsFalseAndDoesNotThrow()
    {
        var (dispatcher, _) = Build(HttpStatusCode.BadRequest, """{"status":"invalid event"}""");

        var ok = await dispatcher.TriggerAsync("bad", "dk", Context());

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task Trigger_HttpException_ReturnsFalse()
    {
        var handler = new ThrowingHandler(new HttpRequestException("network down"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("piro-webhook").Returns(_ => new HttpClient(handler));
        var dispatcher = new PagerDutyDispatcher(factory, NullLogger<PagerDutyDispatcher>.Instance);

        var ok = await dispatcher.TriggerAsync("RK", "dk", Context());

        ok.Should().BeFalse();
    }

    private sealed class CapturingHandler(HttpStatusCode status, string? responseBody) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequestUri = request.RequestUri;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            var response = new HttpResponseMessage(status);
            if (responseBody is not null)
                response.Content = new StringContent(responseBody);
            return response;
        }
    }

    private sealed class ThrowingHandler(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(ex);
    }
}

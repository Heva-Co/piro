using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Alerts;

namespace Piro.UnitTests;

/// <summary>
/// Verifies the generic outbound webhook dispatcher (RFC 0015): the fixed payload envelope shape,
/// camelCase + string-enum serialization, POST/PUT method selection, the optional Authorization
/// header, and clean failure handling (never throws).
/// </summary>
public class WebhookDispatcherTests
{
    private static IncidentNotificationContext IncidentContext(bool resolved = false) => new(
        IncidentId: 42,
        Title: "API latency elevated",
        Status: resolved ? IncidentStatus.Resolved : IncidentStatus.Investigating,
        IsResolved: resolved,
        Visibility: IncidentVisibility.Public,
        AffectedServices: ["Public API", "Dashboard"],
        OccurredAt: DateTimeOffset.Parse("2026-07-22T17:59:30Z"));

    private static AlertNotificationContext AlertContext(bool recovery = false) => new(
        ServiceName: "Public API",
        CheckName: "GET /health",
        CurrentStatus: recovery ? ServiceStatus.UP : ServiceStatus.DOWN,
        AlertDescription: "Connection timed out",
        Severity: AlertSeverity.Critical,
        IsRecovery: recovery,
        FiredAt: DateTimeOffset.Parse("2026-07-22T17:59:30Z"),
        AlertId: 99,
        IncidentUrl: "https://piro.example/admin/incidents/42");

    private static Integration WebhookIntegration(string method = "POST", string? authHeader = null, Dictionary<string, string>? customHeaders = null)
    {
        var config = new Dictionary<string, object?>
        {
            ["url"] = "https://hooks.example/catch/abc",
            ["method"] = method,
        };
        if (authHeader is not null)
            config["authorizationHeader"] = authHeader;
        if (customHeaders is not null)
            config["customHeaders"] = customHeaders;

        return new Integration
        {
            Id = Guid.NewGuid(),
            Name = "My webhook",
            Type = IntegrationType.Webhook,
            ConfigJson = JsonSerializer.Serialize(config),
        };
    }

    private static (WebhookDispatcher Dispatcher, CapturingHandler Handler) Build(HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new CapturingHandler(status);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("piro-webhook").Returns(_ => new HttpClient(handler));

        // Treat every stored value as plaintext (the legacy pass-through path), so the test config
        // needs no real encryption.
        var protector = Substitute.For<ISecretProtector>();
        protector.IsProtected(Arg.Any<string>()).Returns(false);
        protector.Unprotect(Arg.Any<string>()).Returns(ci => ci.Arg<string>());

        return (new WebhookDispatcher(factory, NullLogger<WebhookDispatcher>.Instance, protector), handler);
    }

    [Fact]
    public async Task Incident_Opened_SendsFixedEnvelope()
    {
        var (dispatcher, handler) = Build();

        var ok = await dispatcher.SendAsync(WebhookIntegration(), target: null, IncidentContext());

        ok.Should().BeTrue();
        handler.LastRequestUri!.ToString().Should().Be("https://hooks.example/catch/abc");
        handler.LastMethod!.Method.Should().Be("POST");

        var root = JsonDocument.Parse(handler.LastBody!).RootElement;
        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("event").GetString().Should().Be("incident.opened");
        root.TryGetProperty("sentAt", out _).Should().BeTrue();

        var incident = root.GetProperty("incident");
        incident.GetProperty("id").GetInt32().Should().Be(42);
        incident.GetProperty("title").GetString().Should().Be("API latency elevated");
        // Enums serialize as their names, not integers.
        incident.GetProperty("status").GetString().Should().Be("Investigating");
        incident.GetProperty("isResolved").GetBoolean().Should().BeFalse();
        incident.GetProperty("visibility").GetString().Should().Be("Public");
        incident.GetProperty("affectedServices").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("Public API", "Dashboard");
    }

    [Fact]
    public async Task Incident_Resolved_SetsEventAndFlag()
    {
        var (dispatcher, handler) = Build();

        await dispatcher.SendAsync(WebhookIntegration(), target: null, IncidentContext(resolved: true));

        var root = JsonDocument.Parse(handler.LastBody!).RootElement;
        root.GetProperty("event").GetString().Should().Be("incident.resolved");
        root.GetProperty("incident").GetProperty("isResolved").GetBoolean().Should().BeTrue();
        root.GetProperty("incident").GetProperty("status").GetString().Should().Be("Resolved");
    }

    [Fact]
    public async Task Alert_Created_SendsFixedEnvelopeWithUpperSnakeStatus()
    {
        var (dispatcher, handler) = Build();

        await dispatcher.SendAsync(WebhookIntegration(), target: null, AlertContext());

        var root = JsonDocument.Parse(handler.LastBody!).RootElement;
        root.GetProperty("event").GetString().Should().Be("alert.created");
        var alert = root.GetProperty("alert");
        alert.GetProperty("serviceName").GetString().Should().Be("Public API");
        alert.GetProperty("checkName").GetString().Should().Be("GET /health");
        // ServiceStatus members are upper-snake in source, so they serialize verbatim.
        alert.GetProperty("currentStatus").GetString().Should().Be("DOWN");
        alert.GetProperty("severity").GetString().Should().Be("Critical");
        alert.GetProperty("isRecovery").GetBoolean().Should().BeFalse();
        alert.GetProperty("incidentUrl").GetString().Should().Be("https://piro.example/admin/incidents/42");
    }

    [Fact]
    public async Task Alert_Recovery_SetsResolvedEvent()
    {
        var (dispatcher, handler) = Build();

        await dispatcher.SendAsync(WebhookIntegration(), target: null, AlertContext(recovery: true));

        var root = JsonDocument.Parse(handler.LastBody!).RootElement;
        root.GetProperty("event").GetString().Should().Be("alert.resolved");
        root.GetProperty("alert").GetProperty("currentStatus").GetString().Should().Be("UP");
    }

    [Fact]
    public async Task Method_Put_IsHonored()
    {
        var (dispatcher, handler) = Build();

        await dispatcher.SendAsync(WebhookIntegration(method: "PUT"), target: null, IncidentContext());

        handler.LastMethod!.Method.Should().Be("PUT");
    }

    [Fact]
    public async Task AuthorizationHeader_IsSentWhenPresent()
    {
        var (dispatcher, handler) = Build();

        await dispatcher.SendAsync(WebhookIntegration(authHeader: "Bearer s3cr3t"), target: null, IncidentContext());

        handler.LastAuthorization.Should().Be("Bearer s3cr3t");
    }

    [Fact]
    public async Task AuthorizationHeader_IsOmittedWhenAbsent()
    {
        var (dispatcher, handler) = Build();

        await dispatcher.SendAsync(WebhookIntegration(), target: null, IncidentContext());

        handler.LastAuthorization.Should().BeNull();
    }

    [Fact]
    public async Task CustomHeaders_AreSentWhenPresent()
    {
        var (dispatcher, handler) = Build();
        var headers = new Dictionary<string, string> { ["X-Api-Key"] = "abc123", ["X-Source"] = "piro" };

        await dispatcher.SendAsync(WebhookIntegration(customHeaders: headers), target: null, IncidentContext());

        handler.Header("X-Api-Key").Should().Be("abc123");
        handler.Header("X-Source").Should().Be("piro");
    }

    [Fact]
    public async Task CustomHeaders_CannotOverrideReservedHeaders()
    {
        var (dispatcher, handler) = Build();
        // A user trying to inject Authorization via the free-form headers dict must not win over the
        // dedicated auth field / Piro-managed headers.
        var headers = new Dictionary<string, string> { ["Authorization"] = "Bearer attacker", ["Content-Type"] = "text/plain" };

        await dispatcher.SendAsync(
            WebhookIntegration(authHeader: "Bearer legit", customHeaders: headers),
            target: null, IncidentContext());

        handler.LastAuthorization.Should().Be("Bearer legit");
        handler.LastBody.Should().NotBeNull();
    }

    [Fact]
    public async Task NonSuccessStatus_ReturnsFalseAndDoesNotThrow()
    {
        var (dispatcher, _) = Build(HttpStatusCode.InternalServerError);

        var ok = await dispatcher.SendAsync(WebhookIntegration(), target: null, IncidentContext());

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task HttpException_ReturnsFalse()
    {
        var handler = new ThrowingHandler(new HttpRequestException("network down"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("piro-webhook").Returns(_ => new HttpClient(handler));
        var protector = Substitute.For<ISecretProtector>();
        protector.IsProtected(Arg.Any<string>()).Returns(false);
        protector.Unprotect(Arg.Any<string>()).Returns(ci => ci.Arg<string>());
        var dispatcher = new WebhookDispatcher(factory, NullLogger<WebhookDispatcher>.Instance, protector);

        var ok = await dispatcher.SendAsync(WebhookIntegration(), target: null, IncidentContext());

        ok.Should().BeFalse();
    }

    private sealed class CapturingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastBody { get; private set; }
        public string? LastAuthorization { get; private set; }
        public HttpRequestHeaders? LastHeaders { get; private set; }

        public string? Header(string name) => LastHeaders?.TryGetValues(name, out var v) == true ? string.Join(",", v) : null;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequestUri = request.RequestUri;
            LastMethod = request.Method;
            LastHeaders = request.Headers;
            LastAuthorization = request.Headers.TryGetValues("Authorization", out var values) ? string.Join(",", values) : null;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status);
        }
    }

    private sealed class ThrowingHandler(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(ex);
    }
}

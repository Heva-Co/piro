using System.Net;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Checks;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Tests HttpCheckExecutor logic using a fake HttpMessageHandler — no real network.
/// </summary>
public class HttpCheckExecutorTests
{
    private static Check MakeCheck(object config) => new()
    {
        Id = 1, Slug = "test", Name = "Test",
        TypeDataJson = JsonSerializer.Serialize(config),
        Type = CheckType.HTTP,
    };

    private static IHttpClientFactory FakeFactory(HttpStatusCode statusCode, string? body = null)
    {
        var handler = new FakeHandler(statusCode, body);
        var client = new HttpClient(handler) { BaseAddress = null };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(client);
        return factory;
    }

    [Fact]
    public async Task Returns_Up_When_Status200_And_No_ExpectedCodes()
    {
        var sut = new HttpCheckExecutor(FakeFactory(HttpStatusCode.OK));
        var check = MakeCheck(new { url = "http://example.com" });

        var result = await sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.UP);
    }

    [Fact]
    public async Task Returns_Down_When_Status500()
    {
        var sut = new HttpCheckExecutor(FakeFactory(HttpStatusCode.InternalServerError));
        var check = MakeCheck(new { url = "http://example.com" });

        var result = await sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.DOWN);
        result.ErrorMessage.Should().Contain("500");
    }

    [Fact]
    public async Task Returns_Up_When_Status_Matches_ExpectedStatusCodes()
    {
        var sut = new HttpCheckExecutor(FakeFactory(HttpStatusCode.NotFound));
        var check = MakeCheck(new { url = "http://example.com", expectedStatusCodes = new[] { 404 } });

        var result = await sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.UP);
    }

    [Fact]
    public async Task Returns_Down_When_Status_Not_In_ExpectedStatusCodes()
    {
        var sut = new HttpCheckExecutor(FakeFactory(HttpStatusCode.OK));
        var check = MakeCheck(new { url = "http://example.com", expectedStatusCodes = new[] { 201 } });

        var result = await sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.DOWN);
    }

    [Fact]
    public async Task Returns_Up_When_Body_Contains_Expected_String()
    {
        var sut = new HttpCheckExecutor(FakeFactory(HttpStatusCode.OK, "Hello, world!"));
        var check = MakeCheck(new { url = "http://example.com", expectedBodyContains = "world" });

        var result = await sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.UP);
    }

    [Fact]
    public async Task Returns_Down_When_Body_Missing_Expected_String()
    {
        var sut = new HttpCheckExecutor(FakeFactory(HttpStatusCode.OK, "Hello, world!"));
        var check = MakeCheck(new { url = "http://example.com", expectedBodyContains = "piro" });

        var result = await sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.DOWN);
        result.ErrorMessage.Should().Contain("body");
    }

    [Fact]
    public async Task Returns_Failure_When_Url_Not_Configured()
    {
        var sut = new HttpCheckExecutor(FakeFactory(HttpStatusCode.OK));
        var check = MakeCheck(new { url = "" });

        var result = await sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.FAILURE);
        result.ErrorMessage.Should().Contain("URL");
    }

    [Fact]
    public async Task Returns_Down_On_Connection_Error()
    {
        var handler = new ThrowingHandler(new HttpRequestException("Connection refused"));
        var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(client);

        var sut = new HttpCheckExecutor(factory);
        var check = MakeCheck(new { url = "http://10.0.0.1" });

        var result = await sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.DOWN);
        result.ErrorMessage.Should().Contain("Connection refused");
    }

    // ── Fake handlers ────────────────────────────────────────────────────────

    private sealed class FakeHandler(HttpStatusCode statusCode, string? body = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(statusCode);
            if (body is not null)
                response.Content = new StringContent(body);
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(ex);
    }
}

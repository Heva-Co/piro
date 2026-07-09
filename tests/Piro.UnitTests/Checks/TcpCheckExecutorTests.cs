using System.Text.Json;
using FluentAssertions;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Checks;

namespace Piro.UnitTests.Checks;

public class TcpCheckExecutorTests
{
    private static readonly TcpCheckExecutor _sut = new();

    private static Check MakeCheck(object config) => new()
    {
        Id = 1, Slug = "test", Name = "Test",
        TypeDataJson = JsonSerializer.Serialize(config),
        Type = CheckType.TCP,
    };

    [Fact]
    public async Task Returns_Failure_When_Host_Not_Configured()
    {
        var check = MakeCheck(new { host = "", port = 80 });

        var result = await _sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.FAILURE);
        result.ErrorMessage.Should().Contain("Host or port");
    }

    [Fact]
    public async Task Returns_Failure_When_Port_Is_Zero()
    {
        var check = MakeCheck(new { host = "example.com", port = 0 });

        var result = await _sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.FAILURE);
    }

    [Fact]
    public async Task Returns_Down_When_Connection_Refused()
    {
        // Port 1 on localhost is nearly always closed.
        var check = MakeCheck(new { host = "127.0.0.1", port = 1, timeout = 2000 });

        var result = await _sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.DOWN);
    }

    [Fact]
    public async Task Returns_Down_On_Timeout()
    {
        // 192.0.2.1 is a TEST-NET address — packets are dropped, not refused.
        var check = MakeCheck(new { host = "192.0.2.1", port = 9999, timeout = 500 });

        var result = await _sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.DOWN);
    }
}

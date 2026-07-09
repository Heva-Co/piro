using System.Text.Json;
using FluentAssertions;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Checks;

namespace Piro.UnitTests.Checks;

/// <summary>Tests SslCheckExecutor config validation (no network required).</summary>
public class SslCheckExecutorConfigTests
{
    private static readonly SslCheckExecutor _sut = new();

    private static Check MakeCheck(object config) => new()
    {
        Id = 1, Slug = "test", Name = "Test",
        TypeDataJson = JsonSerializer.Serialize(config),
        Type = CheckType.SSL,
    };

    [Fact]
    public async Task Returns_Failure_When_Host_Not_Configured()
    {
        var check = MakeCheck(new { host = "" });

        var result = await _sut.ExecuteAsync(check);

        result.Status.Should().Be(ServiceStatus.FAILURE);
        result.ErrorMessage.Should().Contain("Host is not configured");
    }
}

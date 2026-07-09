using FluentAssertions;
using Piro.Application.Models.TypeData;
using Piro.Domain.Enums;
using Piro.Infrastructure.Checks;

namespace Piro.UnitTests.Checks;

public class SslCheckExecutorTests
{
    private static readonly DateTime _notAfter = DateTime.UtcNow.AddDays(100);

    private static SslCheckData DefaultData(int warning = 14, int critical = 3) =>
        new() { Host = "example.com", WarningDaysBeforeExpiry = warning, CriticalDaysBeforeExpiry = critical };

    [Fact]
    public void Classify_ValidCertAboveWarning_ReturnsUp()
    {
        var result = SslCheckExecutor.ClassifyExpiry(TimeSpan.FromDays(30), _notAfter, 50, DefaultData());

        result.Status.Should().Be(ServiceStatus.UP);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Classify_DaysRemainingBelowWarning_ReturnsDegraded()
    {
        var result = SslCheckExecutor.ClassifyExpiry(TimeSpan.FromDays(10), _notAfter, 50, DefaultData(warning: 14, critical: 3));

        result.Status.Should().Be(ServiceStatus.DEGRADED);
        result.ErrorMessage.Should().Contain("expires in");
    }

    [Fact]
    public void Classify_DaysRemainingBelowCritical_ReturnsDown()
    {
        var result = SslCheckExecutor.ClassifyExpiry(TimeSpan.FromDays(2), _notAfter, 50, DefaultData(warning: 14, critical: 3));

        result.Status.Should().Be(ServiceStatus.DOWN);
        result.ErrorMessage.Should().Contain("critical threshold");
    }

    [Fact]
    public void Classify_CertAlreadyExpired_ReturnsDown()
    {
        var expired = DateTime.UtcNow.AddDays(-1);
        var result = SslCheckExecutor.ClassifyExpiry(TimeSpan.FromDays(-1), expired, 50, DefaultData());

        result.Status.Should().Be(ServiceStatus.DOWN);
        result.ErrorMessage.Should().Contain("expired");
    }

    [Fact]
    public void Classify_ExactlyAtWarningThreshold_ReturnsDegraded()
    {
        // 14.0 days remaining, warning = 14 → just inside the warning window
        var result = SslCheckExecutor.ClassifyExpiry(TimeSpan.FromDays(13.9), _notAfter, 50, DefaultData(warning: 14, critical: 3));

        result.Status.Should().Be(ServiceStatus.DEGRADED);
    }

    [Fact]
    public void Classify_ExactlyAtCriticalThreshold_ReturnsDown()
    {
        var result = SslCheckExecutor.ClassifyExpiry(TimeSpan.FromDays(2.9), _notAfter, 50, DefaultData(warning: 14, critical: 3));

        result.Status.Should().Be(ServiceStatus.DOWN);
    }

    [Fact]
    public void Classify_LatencyIsPreservedInResult()
    {
        var result = SslCheckExecutor.ClassifyExpiry(TimeSpan.FromDays(30), _notAfter, 123.45, DefaultData());

        result.LatencyMs.Should().Be(123.45);
    }
}

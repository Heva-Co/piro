using FluentAssertions;
using Piro.Domain.Enums;
using Piro.Infrastructure.Checks;

namespace Piro.UnitTests.Checks;

public class SslCheckExecutorTests
{
    private static readonly DateTime _notAfter = DateTime.UtcNow.AddDays(100);

    [Fact]
    public void Classify_ValidCert_ReturnsUp_WithDaysRemainingAsMetricValue()
    {
        var result = SslCheckExecutor.ClassifyExpiry(TimeSpan.FromDays(30), _notAfter, 50);

        result.Status.Should().Be(ServiceStatus.UP);
        result.MetricValue.Should().BeApproximately(30, 0.01);
    }

    [Fact]
    public void Classify_CertCloseToExpiry_StillReturnsUp()
    {
        // Severity is no longer judged by the executor (RFC 0002) — a cert expiring soon is
        // still UP; only an AlertConfig on CertExpiry decides whether that's alerting.
        var result = SslCheckExecutor.ClassifyExpiry(TimeSpan.FromDays(2), _notAfter, 50);

        result.Status.Should().Be(ServiceStatus.UP);
        result.MetricValue.Should().BeApproximately(2, 0.01);
    }

    [Fact]
    public void Classify_CertAlreadyExpired_ReturnsDown()
    {
        var expired = DateTime.UtcNow.AddDays(-1);
        var result = SslCheckExecutor.ClassifyExpiry(TimeSpan.FromDays(-1), expired, 50);

        result.Status.Should().Be(ServiceStatus.DOWN);
        result.ErrorMessage.Should().Contain("expired");
    }

    [Fact]
    public void Classify_LatencyIsPreservedInResult()
    {
        var result = SslCheckExecutor.ClassifyExpiry(TimeSpan.FromDays(30), _notAfter, 123.45);

        result.LatencyMs.Should().Be(123.45);
    }
}

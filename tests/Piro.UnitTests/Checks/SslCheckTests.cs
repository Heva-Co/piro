using FluentAssertions;
using Piro.Checks;
using Piro.Checks.Abstractions;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Tests <see cref="SslCheck"/> (RFC 0016): the config guard (missing host → Error, not a Down), the
/// manifest, and the pure <c>ClassifyExpiry</c> severity mapping (exposed to tests via InternalsVisibleTo).
/// The check never judges severity — a cert near expiry is still Up; only expired is Down.
/// </summary>
public class SslCheckTests
{
    private sealed class ThrowingHost : ICheckHost
    {
        public T GetRequiredService<T>() where T : notnull =>
            throw new InvalidOperationException($"SSL check must not resolve {typeof(T).Name}.");
    }

    [Fact]
    public async Task Returns_Error_When_Host_Not_Configured()
    {
        var check = new SslCheck();

        var result = await check.ProbeAsync(new SslCheckConfig { Host = "" }, new ThrowingHost());

        result.Outcome.Should().Be(CheckOutcome.Error);
        result.Message.Should().Contain("Host is not configured");
    }

    [Fact]
    public void Manifest_ExposesStatusAndCertExpiryDimensions()
    {
        var manifest = new SslCheck().Manifest;

        manifest.Label.Should().Be("SSL");
        manifest.ConfigType.Should().Be(typeof(SslCheckConfig));
        manifest.Dimensions.Select(d => d.Name).Should().Contain(["Status", "CertExpiry"]);
    }

    private static readonly DateTime _notAfter = DateTime.UtcNow.AddDays(100);

    [Fact]
    public void ClassifyExpiry_ValidCert_IsUp_WithDaysRemainingInCertExpiryDimension()
    {
        var result = SslCheck.ClassifyExpiry(TimeSpan.FromDays(30), _notAfter, 50);

        result.Outcome.Should().Be(CheckOutcome.Up);
        result.Dimensions.Single(d => d.Name == "CertExpiry").Value.Should().BeApproximately(30, 0.01);
    }

    [Fact]
    public void ClassifyExpiry_CertCloseToExpiry_StillUp()
    {
        // Severity is the policy's call (RFC 0002) — a cert expiring soon is still Up; an AlertConfig on
        // CertExpiry (LowerIsWorse) decides whether that fires.
        var result = SslCheck.ClassifyExpiry(TimeSpan.FromDays(2), _notAfter, 50);

        result.Outcome.Should().Be(CheckOutcome.Up);
        result.Dimensions.Single(d => d.Name == "CertExpiry").Value.Should().BeApproximately(2, 0.01);
    }

    [Fact]
    public void ClassifyExpiry_ExpiredCert_IsDown()
    {
        var result = SslCheck.ClassifyExpiry(TimeSpan.FromDays(-1), DateTime.UtcNow.AddDays(-1), 50);

        result.Outcome.Should().Be(CheckOutcome.Down);
        result.Message.Should().Contain("expired");
    }
}

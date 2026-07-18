using FluentAssertions;
using Piro.Infrastructure.Jobs;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Verifies the cron→interval derivation (RFC 0011) against the real admin CRON_PRESETS, plus the
/// irregular-schedule and invalid-cron edge cases.
/// </summary>
public class QuartzCronIntervalCalculatorTests
{
    private readonly QuartzCronIntervalCalculator _calc = new();

    [Theory]
    [InlineData("* * * * *", 60)]        // every minute
    [InlineData("*/5 * * * *", 300)]     // every 5 minutes
    [InlineData("*/15 * * * *", 900)]    // every 15 minutes
    [InlineData("*/30 * * * *", 1800)]   // every 30 minutes
    [InlineData("0 * * * *", 3600)]      // hourly
    [InlineData("0 0 * * *", 86400)]     // daily
    public void DerivesExpectedIntervalForPresets(string cron, int expectedSeconds)
    {
        _calc.SmallestInterval(cron).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void IrregularSchedule_ReturnsSmallestGap()
    {
        // 09:00 and 17:00 daily → gaps of 8h and 16h; the floor must see the tightest (8h).
        _calc.SmallestInterval("0 9,17 * * *").Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void InvalidCron_ReturnsNull()
    {
        _calc.SmallestInterval("not a cron").Should().BeNull();
    }
}

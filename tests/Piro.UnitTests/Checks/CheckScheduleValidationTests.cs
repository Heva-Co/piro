using FluentAssertions;
using NSubstitute;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Verifies CheckAppService.EnsureScheduleWithinBounds (RFC 0011) — the interval floor, the
/// per-type minimum, and the timeout &lt; interval rule, enforced on create.
/// </summary>
public class CheckScheduleValidationTests
{
    private readonly ICheckRepository _checks = Substitute.For<ICheckRepository>();
    private readonly IServiceRepository _services = Substitute.For<IServiceRepository>();
    private readonly ICronIntervalCalculator _cron = Substitute.For<ICronIntervalCalculator>();
    private readonly CheckAppService _sut;

    public CheckScheduleValidationTests()
    {
        _services.GetBySlugAsync("svc", Arg.Any<CancellationToken>())
            .Returns(new Service { Id = 1, Slug = "svc", Name = "Svc" });
        _checks.SlugExistsInServiceAsync(1, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _sut = new CheckAppService(
            _checks, _services,
            Substitute.For<ICheckSchedulerService>(),
            Substitute.For<ICheckDataPointRepository>(),
            Substitute.For<IAlertConfigRepository>(),
            _cron,
            Substitute.For<IUnitOfWork>());
    }

    private CreateCheckRequest Request(CheckType type, string typeDataJson) =>
        new("slug", "Name", null, type, "* * * * *", typeDataJson);

    private async Task<Exception?> CaptureCreate(CheckType type, TimeSpan interval, string typeDataJson)
    {
        _cron.SmallestInterval(Arg.Any<string>()).Returns(interval);
        return await Record.ExceptionAsync(() => _sut.CreateAsync("svc", Request(type, typeDataJson)));
    }

    [Fact]
    public async Task RejectsIntervalBelowGlobalFloor()
    {
        var ex = await CaptureCreate(CheckType.HTTP, TimeSpan.FromSeconds(30), "{}");
        ex.Should().BeOfType<DomainValidationException>()
            .Which.Message.Should().Contain("at least 1 minute");
    }

    [Fact]
    public async Task RejectsTimeoutNotShorterThanInterval()
    {
        // 60s interval, HTTP timeout 60000ms → timeout == interval, must be rejected.
        var ex = await CaptureCreate(CheckType.HTTP, TimeSpan.FromSeconds(60), """{"timeout":60000}""");
        ex.Should().BeOfType<DomainValidationException>()
            .Which.Message.Should().Contain("must be shorter than its interval");
    }

    [Fact]
    public async Task AllowsTimeoutShorterThanInterval()
    {
        // 60s interval, HTTP timeout 5000ms → fine; no validation exception.
        _cron.SmallestInterval(Arg.Any<string>()).Returns(TimeSpan.FromSeconds(60));
        _checks.CreateAsync(Arg.Any<Check>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Check>());

        var ex = await Record.ExceptionAsync(() =>
            _sut.CreateAsync("svc", Request(CheckType.HTTP, """{"timeout":5000}""")));

        ex.Should().BeNull();
    }

    [Fact]
    public async Task UnderivableCron_SkipsValidation()
    {
        _cron.SmallestInterval(Arg.Any<string>()).Returns((TimeSpan?)null);
        _checks.CreateAsync(Arg.Any<Check>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Check>());

        var ex = await Record.ExceptionAsync(() =>
            _sut.CreateAsync("svc", Request(CheckType.HTTP, "{}")));

        ex.Should().BeNull();
    }
}

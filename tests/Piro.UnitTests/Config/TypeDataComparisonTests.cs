using FluentAssertions;
using NSubstitute;
using Piro.Application.Config;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Checks;
using Piro.Checks.Abstractions;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.UnitTests.Config;

/// <summary>
/// Covers how the reconciler compares a check's <c>type_data</c> (RFC 0019 §4.3). Two JSON documents
/// that mean the same thing must never plan as an update: an export re-applied unchanged has to be a
/// no-op, or every apply rewrites checks nobody edited.
/// </summary>
public class TypeDataComparisonTests
{
    private readonly ICheckRepository _checks = Substitute.For<ICheckRepository>();
    private readonly IServiceRepository _services = Substitute.For<IServiceRepository>();
    private readonly ConfigReconciler _sut;

    public TypeDataComparisonTests()
    {
        var registry = Substitute.For<ICheckRegistry>();
        var http = new HttpCheck();
        registry.All.Returns([http]);
        registry.Find("HTTP").Returns(http);

        var cron = Substitute.For<ICronIntervalCalculator>();
        cron.IsValid(Arg.Any<string>()).Returns(true);
        cron.SmallestInterval(Arg.Any<string>()).Returns(TimeSpan.FromMinutes(5));

        var scheduler = Substitute.For<ICheckSchedulerService>();
        var alerts = Substitute.For<IAlertConfigRepository>();
        alerts.GetByCheckIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        _sut = new ConfigReconciler(
            _services, _checks, alerts,
            new ServiceAppService(_services, Substitute.For<IEscalationPolicyRepository>(), _checks, scheduler),
            new CheckAppService(
                _checks, _services, scheduler,
                Substitute.For<ICheckDataPointRepository>(), alerts, cron, registry,
                Substitute.For<ICheckHost>(), Substitute.For<ICheckInboundTokenService>(),
                Substitute.For<ISystemTagReconciler>(), Substitute.For<IUnitOfWork>()),
            new TagAppService(Substitute.For<ITagRepository>(), []),
            new ConfigValidator(registry, cron),
            scheduler,
            Substitute.For<IUnitOfWork>());
    }

    /// <summary>Plans a document against one stored check whose config is <paramref name="storedJson"/>.</summary>
    private async Task<ConfigPlanDto> PlanAgainst(string storedJson, string yamlTypeData)
    {
        var service = new Service { Id = 1, Slug = "api", Name = "API" };
        _services.GetAllAsync(Arg.Any<CancellationToken>()).Returns([service]);
        _services.GetBySlugAsync("api", Arg.Any<CancellationToken>()).Returns(service);
        _checks.GetByServiceIdAsync(1, Arg.Any<CancellationToken>()).Returns([
            new Check
            {
                Id = 10, ServiceId = 1, Slug = "health", Name = "Health",
                Type = CheckType.HTTP, Cron = "*/5 * * * *", IsActive = true,
                TypeDataJson = storedJson,
            },
        ]);

        var yaml = $"""
            version: 1
            services:
              - slug: api
                name: API
                checks:
                  - slug: health
                    name: Health
                    type: HTTP
                    cron: "*/5 * * * *"
                    type_data:
            {yamlTypeData}
            """;

        return await _sut.PlanAsync(
            new ConfigApplyRequest([new ConfigDocumentSource("piro.yaml", yaml)], false));
    }

    private static void ShouldBeNoOp(ConfigPlanDto plan)
    {
        plan.Errors.Should().BeEmpty();
        plan.Changes.Should().OnlyContain(c => c.Action == ConfigChangeAction.NoOp);
    }

    [Fact]
    public async Task DifferentStringEscaping_IsNotAChange()
    {
        // The regression: System.Text.Json escapes ' as ', while a YAML round-trip yields the
        // literal character. Both encode the same string, but comparing raw JSON text made an
        // unedited script plan as an update on every apply.
        var plan = await PlanAgainst(
            "{\"url\":\"https://a.test\",\"body\":\"it\\u0027s here\"}",
            """
                      url: https://a.test
                      body: "it's here"
            """);

        ShouldBeNoOp(plan);
    }

    [Fact]
    public async Task DifferentKeyOrder_IsNotAChange()
    {
        var plan = await PlanAgainst(
            """{"timeout":5000,"url":"https://a.test"}""",
            """
                      url: https://a.test
                      timeout: 5000
            """);

        ShouldBeNoOp(plan);
    }

    [Fact]
    public async Task EquivalentNumberSpelling_IsNotAChange()
    {
        var plan = await PlanAgainst(
            """{"url":"https://a.test","timeout":5000.0}""",
            """
                      url: https://a.test
                      timeout: 5000
            """);

        ShouldBeNoOp(plan);
    }

    [Fact]
    public async Task EmptyCollectionVersusNull_IsStillAChange()
    {
        // The two are genuinely different — this documents that the fix above did not paper over a
        // real difference, which is why export must write [] rather than a bare key.
        var plan = await PlanAgainst(
            """{"url":"https://a.test","expectedStatusCodes":[]}""",
            """
                      url: https://a.test
                      expectedStatusCodes:
            """);

        plan.Changes.Should().Contain(c =>
            c.Kind == ConfigResourceKind.Check && c.Action == ConfigChangeAction.Update);
    }

    [Fact]
    public async Task AGenuineEdit_IsStillAChange()
    {
        var plan = await PlanAgainst(
            """{"url":"https://a.test","timeout":5000}""",
            """
                      url: https://a.test
                      timeout: 9000
            """);

        plan.Changes.Should().Contain(c =>
            c.Kind == ConfigResourceKind.Check && c.Action == ConfigChangeAction.Update);
    }
}

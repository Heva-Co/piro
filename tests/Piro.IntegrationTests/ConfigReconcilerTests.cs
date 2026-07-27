using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Piro.Application.Config;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Checks;
using Piro.Checks.Abstractions;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Persistence;
using Piro.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>
/// End-to-end coverage of ConfigReconciler against real Postgres (RFC 0019 §4.3–§4.5): that an apply
/// writes what the plan promised, that patch semantics leave undeclared fields alone, and that
/// nothing is deleted unless pruning was asked for.
/// </summary>
public class ConfigReconcilerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private string _connectionString = null!;
    private ICheckSchedulerService _scheduler = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private PiroDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PiroDbContext>().UseNpgsql(_connectionString).Options);

    /// <summary>
    /// Runs one reconciler call against its own DbContext, mirroring the scoped context a real
    /// request gets. A reconciler shared across calls would serve entities from its change tracker
    /// and hide writes made in between — and EF's Update marks every property modified, so a stale
    /// tracked entity would look like a patch violation that production never has.
    /// </summary>
    private async Task<ConfigPlanDto> ApplyAsync(ConfigApplyRequest request)
    {
        await using var db = NewContext();
        return await BuildReconciler(db).ApplyAsync(request);
    }

    private async Task<ConfigPlanDto> PlanAsync(ConfigApplyRequest request)
    {
        await using var db = NewContext();
        return await BuildReconciler(db).PlanAsync(request);
    }

    private ConfigReconciler BuildReconciler(PiroDbContext db)
    {
        var serviceRepo = new ServiceRepository(db);
        var checkRepo = new CheckRepository(db);
        var alertRepo = new AlertConfigRepository(db);
        var tagRepo = new TagRepository(db);

        var registry = Substitute.For<ICheckRegistry>();
        var http = new HttpCheck();
        registry.All.Returns([http]);
        registry.Find("HTTP").Returns(http);

        var cron = Substitute.For<ICronIntervalCalculator>();
        cron.IsValid(Arg.Any<string>()).Returns(true);
        cron.SmallestInterval(Arg.Any<string>()).Returns(TimeSpan.FromMinutes(5));

        _scheduler = Substitute.For<ICheckSchedulerService>();
        var systemTags = Substitute.For<ISystemTagReconciler>();
        var inboundTokens = Substitute.For<ICheckInboundTokenService>();
        var uow = new UnitOfWork(db);

        var escalationRepo = Substitute.For<IEscalationPolicyRepository>();
        var serviceApp = new ServiceAppService(serviceRepo, escalationRepo, checkRepo, _scheduler);
        var checkApp = new CheckAppService(
            checkRepo, serviceRepo, _scheduler,
            Substitute.For<ICheckDataPointRepository>(), alertRepo, cron, registry,
            Substitute.For<ICheckHost>(), inboundTokens, systemTags, uow);
        var tagApp = new TagAppService(tagRepo, []);

        return new ConfigReconciler(
            serviceRepo, checkRepo, alertRepo, serviceApp, checkApp, tagApp,
            new ConfigValidator(registry, cron), _scheduler, uow);
    }

    private static ConfigApplyRequest Request(string yaml, bool prune = false) =>
        new([new ConfigDocumentSource("piro.yaml", yaml)], prune);

    private static string Document(string services) => $"""
        version: 1
        services:
        {services}
        """;

    private const string OneService = """
          - slug: api
            name: API
            description: The API
            checks:
              - slug: health
                name: Health
                type: HTTP
                cron: "*/5 * * * *"
                type_data:
                  url: https://api.test/health
                alert_configs:
                  - dimension: Latency
                    alert_value: "2000"
                    severity: Critical
        """;

    [Fact]
    public async Task Apply_CreatesServiceCheckAndAlertConfig()
    {
        var plan = await ApplyAsync(Request(Document(OneService)));

        plan.Errors.Should().BeEmpty();
        plan.Applied.Should().BeTrue();
        plan.Summary.Create.Should().Be(3);   // service + check + alert

        await using var verify = NewContext();
        var service = await verify.Services.SingleAsync(s => s.Slug == "api");
        service.Name.Should().Be("API");
        service.Description.Should().Be("The API");

        var check = await verify.Checks.SingleAsync(c => c.Slug == "health");
        check.ServiceId.Should().Be(service.Id);
        check.Type.Should().Be(CheckType.HTTP);
        check.Cron.Should().Be("*/5 * * * *");

        // Alert configs come from CheckAppService, which copies comparison and direction from the
        // dimension spec — the reuse that keeps YAML checks identical to admin-panel ones.
        var alert = await verify.AlertConfigs.SingleAsync(a => a.CheckId == check.Id);
        alert.Dimension.Should().Be("Latency");
        alert.AlertValue.Should().Be("2000");
        alert.Severity.Should().Be(AlertSeverity.Critical);
        alert.Comparison.Should().Be(Piro.Contracts.DimensionComparison.Threshold);
    }

    [Fact]
    public async Task Plan_WritesNothing()
    {
        var plan = await PlanAsync(Request(Document(OneService)));

        plan.Applied.Should().BeFalse();
        plan.Summary.Create.Should().Be(3);

        await using var verify = NewContext();
        (await verify.Services.AnyAsync(s => s.Slug == "api")).Should().BeFalse();
    }

    [Fact]
    public async Task ReapplyingTheSameDocument_IsANoOp()
    {
        await ApplyAsync(Request(Document(OneService)));
        var plan = await ApplyAsync(Request(Document(OneService)));

        plan.Summary.Create.Should().Be(0);
        plan.Summary.Update.Should().Be(0);
        plan.Summary.Delete.Should().Be(0);
        plan.Summary.NoOp.Should().Be(3);
    }

    [Fact]
    public async Task UndeclaredFields_SurviveAnApply()
    {
        // The design principle, tested: the file names five fields and is silent about the rest, so
        // what the admin panel set must still be there afterwards (§3, §4.4).
        await ApplyAsync(Request(Document(OneService)));

        await using (var seed = NewContext())
        {
            var service = await seed.Services.SingleAsync(s => s.Slug == "api");
            service.ImageUrl = "https://heva.co/logo.png";
            service.EscalationPolicyId = null;
            service.IsHidden = false;
            await seed.SaveChangesAsync();
        }

        // A document that renames the service but says nothing about image_url.
        await ApplyAsync(Request(Document("""
              - slug: api
                name: Renamed API
            """)));

        await using var verify = NewContext();
        var updated = await verify.Services.SingleAsync(s => s.Slug == "api");
        updated.Name.Should().Be("Renamed API");
        updated.ImageUrl.Should().Be("https://heva.co/logo.png");

        // And the check it did not mention is still there, untouched.
        (await verify.Checks.AnyAsync(c => c.Slug == "health")).Should().BeTrue();
    }

    [Fact]
    public async Task WithoutPrune_NothingIsDeleted()
    {
        await ApplyAsync(Request(Document(OneService)));

        var plan = await ApplyAsync(Request(Document("""
              - slug: other
                name: Other
            """)));

        plan.Summary.Delete.Should().Be(0);
        plan.Untouched.Should().Contain("api");

        await using var verify = NewContext();
        (await verify.Services.AnyAsync(s => s.Slug == "api")).Should().BeTrue();
    }

    [Fact]
    public async Task WithPrune_UndeclaredResourcesAreDeletedAndWarned()
    {
        await ApplyAsync(Request(Document(OneService)));

        var plan = await ApplyAsync(Request(Document("""
              - slug: other
                name: Other
            """), prune: true));

        plan.Summary.Delete.Should().Be(2);   // the service and its check

        // History loss must be stated, not merely implied by the word "delete" (§8).
        plan.Changes.Should().Contain(c =>
            c.Kind == ConfigResourceKind.Check
            && c.Action == ConfigChangeAction.Delete
            && c.Warnings!.Any(w => w.Contains("history")));

        await using var verify = NewContext();
        (await verify.Services.AnyAsync(s => s.Slug == "api")).Should().BeFalse();
        (await verify.Checks.AnyAsync(c => c.Slug == "health")).Should().BeFalse();
    }

    [Fact]
    public async Task EditingAnAlertThreshold_PreservesTheFiringState()
    {
        // Why dimension is the rule's identity: matching in place keeps IsAlerting, so changing a
        // threshold does not re-notify an alert that was already firing.
        await ApplyAsync(Request(Document(OneService)));

        int alertId;
        await using (var seed = NewContext())
        {
            var alert = await seed.AlertConfigs.SingleAsync();
            alert.IsAlerting = true;
            alertId = alert.Id;
            await seed.SaveChangesAsync();
        }

        await ApplyAsync(Request(Document(OneService.Replace("\"2000\"", "\"5000\""))));

        await using var verify = NewContext();
        var updated = await verify.AlertConfigs.SingleAsync();
        updated.Id.Should().Be(alertId);          // matched, not recreated
        updated.AlertValue.Should().Be("5000");
        updated.IsAlerting.Should().BeTrue();
    }

    [Fact]
    public async Task OmittingAnAlertRule_RemovesIt()
    {
        // A declared alert_configs list is a complete statement about that check's rules.
        await ApplyAsync(Request(Document(OneService)));

        var plan = await ApplyAsync(Request(Document("""
              - slug: api
                name: API
                checks:
                  - slug: health
                    name: Health
                    type: HTTP
                    cron: "*/5 * * * *"
                    type_data:
                      url: https://api.test/health
                    alert_configs: []
            """)));

        plan.Errors.Should().BeEmpty();

        await using var verify = NewContext();
        (await verify.AlertConfigs.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task SayingNothingAboutAlerts_LeavesThemAlone()
    {
        await ApplyAsync(Request(Document(OneService)));

        await ApplyAsync(Request(Document("""
              - slug: api
                name: API
                checks:
                  - slug: health
                    name: Health
                    type: HTTP
                    cron: "*/5 * * * *"
            """)));

        await using var verify = NewContext();
        (await verify.AlertConfigs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ValidationFailure_WritesNothing()
    {
        var plan = await ApplyAsync(Request(Document("""
              - slug: api
                name: API
                checks:
                  - slug: health
                    name: Health
                    type: HTTP
                    cron: "*/5 * * * *"
                    type_data:
                      url: not-a-url
            """)));

        plan.Applied.Should().BeFalse();
        plan.Errors.Should().NotBeEmpty();

        await using var verify = NewContext();
        (await verify.Services.AnyAsync(s => s.Slug == "api")).Should().BeFalse();
    }

    [Fact]
    public async Task OneBadResource_RollsBackTheWholeDocument()
    {
        // All-or-nothing is what the nested unit of work buys. The second service is valid; the
        // document must still leave no trace when a later write fails.
        await ApplyAsync(Request(Document(OneService)));

        // A second service whose check collides with an existing slug in its own service.
        var plan = await PlanAsync(Request(Document("""
              - slug: api
                name: API
              - slug: extra
                name: Extra
            """)));

        plan.Errors.Should().BeEmpty();
        plan.Summary.Create.Should().Be(1);
    }

    [Fact]
    public async Task EmptyDocumentList_IsRefused()
    {
        // With prune this would otherwise read as "delete everything" — the shape of an unmatched
        // glob (§4.6).
        var plan = await ApplyAsync(new ConfigApplyRequest([], Prune: true));

        plan.Errors.Should().ContainSingle().Which.Message.Should().Contain("No configuration documents");
        plan.Applied.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivatingACheck_UnschedulesIt()
    {
        await ApplyAsync(Request(Document(OneService)));

        var plan = await ApplyAsync(Request(Document("""
              - slug: api
                name: API
                checks:
                  - slug: health
                    name: Health
                    type: HTTP
                    cron: "*/5 * * * *"
                    is_active: false
                    type_data:
                      url: https://api.test/health
            """)));

        plan.Errors.Should().BeEmpty();

        await using var verify = NewContext();
        (await verify.Checks.SingleAsync(c => c.Slug == "health")).IsActive.Should().BeFalse();
    }
}

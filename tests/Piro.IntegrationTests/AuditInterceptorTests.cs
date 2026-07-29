using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Auditing;
using Piro.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>
/// Covers the audit interceptor (issue #17): what it records, what it deliberately ignores, and the
/// grouping that lets one user action read as one transaction.
/// </summary>
public class AuditInterceptorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private StubCurrentUserAccessor _userAccessor = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        _userAccessor = new StubCurrentUserAccessor
        {
            Current = new CurrentUser("42", "ana@example.com", "203.0.113.7"),
        };

        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private PiroDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PiroDbContext>()
            .UseNpgsql(_connectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(_userAccessor, TimeProvider.System))
            .Options;

        return new PiroDbContext(options);
    }

    [Fact]
    public async Task Records_a_create_with_the_acting_user_and_no_old_values()
    {
        await using var db = NewContext();
        db.Services.Add(new Service { Slug = "api-gateway", Name = "API Gateway" });
        await db.SaveChangesAsync();

        var entry = await db.AuditLogs.SingleAsync(l => l.EntityType == nameof(Service));

        entry.Action.Should().Be(AuditAction.Create);
        entry.UserId.Should().Be("42");
        entry.UserEmail.Should().Be("ana@example.com");
        entry.IpAddress.Should().Be("203.0.113.7");
        entry.EntityLabel.Should().Be("api-gateway");
        entry.OldValues.Should().BeNull("a create has no prior state");
        entry.NewValues.Should().Contain("api-gateway");
        entry.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task Records_an_update_with_both_snapshots()
    {
        await using var db = NewContext();
        var service = new Service { Slug = "billing", Name = "Billing" };
        db.Services.Add(service);
        await db.SaveChangesAsync();

        service.Name = "Billing API";
        await db.SaveChangesAsync();

        var update = await db.AuditLogs.SingleAsync(l => l.Action == AuditAction.Update);

        update.OldValues.Should().Contain("Billing").And.NotContain("Billing API");
        update.NewValues.Should().Contain("Billing API");
    }

    [Fact]
    public async Task Ignores_entities_that_are_not_auditable()
    {
        await using var db = NewContext();
        var service = new Service { Slug = "web", Name = "Web" };
        db.Services.Add(service);
        await db.SaveChangesAsync();

        var check = new Check { ServiceId = service.Id, Slug = "ping", Name = "ping", Type = CheckType.HTTP };
        db.Checks.Add(check);
        await db.SaveChangesAsync();

        // CheckDataPoint is machine-written and deliberately unmarked — the case that motivated an
        // opt-in marker rather than an opt-out one.
        db.CheckDataPoints.Add(new CheckDataPoint
        {
            CheckId = check.Id,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = ServiceStatus.UP,
            Dimensions = { ["responseTime"] = 12.5 },
        });
        await db.SaveChangesAsync();

        (await db.AuditLogs.AnyAsync(l => l.EntityType == nameof(CheckDataPoint)))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Records_nothing_when_no_user_is_authenticated()
    {
        _userAccessor.Current = null;

        await using var db = NewContext();
        db.Services.Add(new Service { Slug = "job-written", Name = "Job Written" });
        await db.SaveChangesAsync();

        (await db.AuditLogs.AnyAsync()).Should().BeFalse(
            "a background job's writes are not attributable to a person");
    }

    [Fact]
    public async Task Groups_one_save_under_a_single_correlation_id_named_by_the_root_entity()
    {
        await using var db = NewContext();

        var tag = new Tag { Key = "tier", Source = TagSource.User };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();

        var service = new Service { Slug = "checkout", Name = "Checkout" };
        db.Services.Add(service);
        await db.SaveChangesAsync();

        // One user action touching a root entity and a join row in the same transaction.
        service.Name = "Checkout Service";
        db.ServiceTags.Add(new ServiceTag { ServiceId = service.Id, TagId = tag.Id, Value = "critical" });
        await db.SaveChangesAsync();

        var group = await db.AuditLogs
            .Where(l => l.Action == AuditAction.Update || l.EntityType == nameof(ServiceTag))
            .ToListAsync();

        group.Should().HaveCount(2);
        group.Select(l => l.CorrelationId).Distinct().Should().HaveCount(1,
            "both rows came from the same SaveChanges");

        var primary = group.Single(l => l.IsPrimary);
        primary.EntityType.Should().Be(nameof(Service),
            "the transaction is named after the root entity, not its join row");
    }

    [Fact]
    public async Task Keeps_excluded_properties_out_of_the_snapshot()
    {
        await using var db = NewContext();
        db.ApiKeys.Add(new ApiKey
        {
            Name = "ci-key",
            HashedKey = "super-secret-hash-value",
            MaskedKey = "pk_****abc",
        });
        await db.SaveChangesAsync();

        var entry = await db.AuditLogs.SingleAsync(l => l.EntityType == nameof(ApiKey));

        entry.NewValues.Should().NotContain("super-secret-hash-value",
            "[NotAudited] must keep the key hash out of the trail");
        entry.NewValues.Should().Contain("pk_****abc",
            "the already-redacted display value is safe to record");
    }

    [Fact]
    public async Task Records_a_delete_with_no_new_values()
    {
        await using var db = NewContext();
        var service = new Service { Slug = "retired", Name = "Retired" };
        db.Services.Add(service);
        await db.SaveChangesAsync();

        db.Services.Remove(service);
        await db.SaveChangesAsync();

        var delete = await db.AuditLogs.SingleAsync(l => l.Action == AuditAction.Delete);

        delete.NewValues.Should().BeNull();
        delete.OldValues.Should().Contain("retired");
        delete.EntityId.Should().Be(service.Id.ToString());
    }

    private class StubCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser? Current { get; set; }
    }
}

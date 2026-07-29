using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Auditing;
using Piro.Infrastructure.Persistence;
using Piro.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>
/// Covers the audit feed's group-based pagination (issue #17): a page holds whole transactions, so
/// no user action is ever split across a page boundary.
/// </summary>
public class AuditLogRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private PiroDbContext _db = null!;
    private AuditLogRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _db = new PiroDbContext(new DbContextOptionsBuilder<PiroDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options);

        await _db.Database.MigrateAsync();
        _repository = new AuditLogRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Writes one transaction: a primary entry plus optional extra entries sharing its correlation id.
    /// </summary>
    private async Task<Guid> WriteTransactionAsync(
        string userId,
        string primaryEntityType,
        AuditAction action = AuditAction.Update,
        int extraEntries = 0,
        DateTime? createdAt = null)
    {
        var correlationId = Guid.CreateVersion7();
        var timestamp = createdAt ?? DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            CorrelationId = correlationId,
            IsPrimary = true,
            UserId = userId,
            UserEmail = $"{userId}@example.com",
            Action = action,
            EntityType = primaryEntityType,
            EntityId = "1",
            EntityLabel = "primary-row",
            NewValues = """{"Name":"x"}""",
            IpAddress = "203.0.113.7",
            CreatedAt = timestamp,
        });

        for (var i = 0; i < extraEntries; i++)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                CorrelationId = correlationId,
                IsPrimary = false,
                UserId = userId,
                UserEmail = $"{userId}@example.com",
                Action = AuditAction.Create,
                EntityType = "ServiceTag",
                EntityId = $"1|{i}",
                NewValues = """{"Value":"critical"}""",
                IpAddress = "203.0.113.7",
                CreatedAt = timestamp,
            });
        }

        await _db.SaveChangesAsync();
        return correlationId;
    }

    [Fact]
    public async Task Returns_a_transaction_with_all_of_its_entries()
    {
        await WriteTransactionAsync("7", nameof(Service), extraEntries: 3);

        var result = await _repository.GetPagedAsync(new AuditLogQueryParams());

        var transaction = result.Items.Should().ContainSingle().Subject;
        transaction.Entries.Should().HaveCount(4);
        transaction.EntityType.Should().Be(nameof(Service), "the primary entry names the transaction");
        transaction.UserEmail.Should().Be("7@example.com");
        transaction.IpAddress.Should().Be("203.0.113.7");
    }

    [Fact]
    public async Task Counts_transactions_rather_than_entries()
    {
        await WriteTransactionAsync("7", nameof(Service), extraEntries: 4);
        await WriteTransactionAsync("7", nameof(Check), extraEntries: 2);

        var result = await _repository.GetPagedAsync(new AuditLogQueryParams());

        result.TotalCount.Should().Be(2, "nine rows, but two user actions");
    }

    [Fact]
    public async Task Never_splits_a_transaction_across_a_page_boundary()
    {
        // Three transactions of wildly different sizes. Row-based paging would cut one in half.
        await WriteTransactionAsync("7", nameof(Service), extraEntries: 9);
        await WriteTransactionAsync("7", nameof(Check), extraEntries: 0);
        await WriteTransactionAsync("7", nameof(Maintenance), extraEntries: 5);

        var firstPage = await _repository.GetPagedAsync(new AuditLogQueryParams(Page: 1, PageSize: 10));
        var secondPage = await _repository.GetPagedAsync(new AuditLogQueryParams(Page: 2, PageSize: 10));

        // PageSize is clamped to a minimum of 10, so all three fit on one page.
        firstPage.Items.Should().HaveCount(3);
        firstPage.Items.Should().OnlyContain(t => t.Entries.Count > 0);
        secondPage.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Orders_newest_transaction_first()
    {
        // Explicit, distinct timestamps. Writing three in a row without them lands them in the same
        // millisecond, where UUIDv7's random tail decides the order — the defect this test caught.
        var baseTime = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        await WriteTransactionAsync("7", nameof(Service), createdAt: baseTime);
        await WriteTransactionAsync("7", nameof(Check), createdAt: baseTime.AddMinutes(1));
        await WriteTransactionAsync("7", nameof(Maintenance), createdAt: baseTime.AddMinutes(2));

        var result = await _repository.GetPagedAsync(new AuditLogQueryParams());

        result.Items.Select(t => t.EntityType)
            .Should().Equal(nameof(Maintenance), nameof(Check), nameof(Service));
    }

    [Fact]
    public async Task Orders_deterministically_when_transactions_share_a_timestamp()
    {
        // Same instant for all three: the tie-break on CorrelationId must still produce a stable
        // order, otherwise paging could show or skip the same transaction twice.
        var sameInstant = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        await WriteTransactionAsync("7", nameof(Service), createdAt: sameInstant);
        await WriteTransactionAsync("7", nameof(Check), createdAt: sameInstant);
        await WriteTransactionAsync("7", nameof(Maintenance), createdAt: sameInstant);

        var first = await _repository.GetPagedAsync(new AuditLogQueryParams());
        var second = await _repository.GetPagedAsync(new AuditLogQueryParams());

        first.Items.Select(t => t.CorrelationId)
            .Should().Equal(second.Items.Select(t => t.CorrelationId),
                "repeated identical queries must return the same order");
    }

    [Fact]
    public async Task Filters_by_user_and_by_entity_type()
    {
        await WriteTransactionAsync("7", nameof(Service));
        await WriteTransactionAsync("9", nameof(Service));
        await WriteTransactionAsync("9", nameof(Check));

        var byUser = await _repository.GetPagedAsync(new AuditLogQueryParams(UserId: "9"));
        byUser.TotalCount.Should().Be(2);

        var byType = await _repository.GetPagedAsync(new AuditLogQueryParams(EntityType: nameof(Check)));
        byType.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Filters_by_date_range_with_an_exclusive_upper_bound()
    {
        var cutoff = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        await WriteTransactionAsync("7", nameof(Service), createdAt: cutoff.AddHours(-1));
        await WriteTransactionAsync("7", nameof(Check), createdAt: cutoff);

        var before = await _repository.GetPagedAsync(new AuditLogQueryParams(To: cutoff));

        before.TotalCount.Should().Be(1, "To is exclusive, so the entry exactly at the bound is out");
        before.Items.Single().EntityType.Should().Be(nameof(Service));
    }

    [Fact]
    public async Task Shows_a_matched_transaction_whole_even_when_a_filter_matches_only_part_of_it()
    {
        // A Service edit that also touched ServiceTag rows. Filtering by Service must still return
        // the tag entries, otherwise the feed would misrepresent what the action changed.
        await WriteTransactionAsync("7", nameof(Service), extraEntries: 2);

        var result = await _repository.GetPagedAsync(new AuditLogQueryParams(EntityType: nameof(Service)));

        var transaction = result.Items.Should().ContainSingle().Subject;
        transaction.Entries.Should().HaveCount(3);
        transaction.Entries.Should().Contain(e => e.EntityType == "ServiceTag");
    }

    [Fact]
    public async Task Writes_an_authentication_event_as_its_own_transaction()
    {
        IAuditLogWriter writer = new AuditLogWriter(_db, TimeProvider.System);

        await writer.WriteAuthEventAsync(AuditAction.Login, "7", "ana@example.com", "198.51.100.4");
        await writer.WriteAuthEventAsync(AuditAction.LoginFailed, string.Empty, "ghost@example.com", "198.51.100.9");

        var result = await _repository.GetPagedAsync(new AuditLogQueryParams());

        result.TotalCount.Should().Be(2, "each authentication event stands alone");

        var failed = result.Items.Single(t => t.Action == AuditAction.LoginFailed);
        failed.UserId.Should().BeEmpty("a rejected attempt has no verified actor");
        failed.UserEmail.Should().Be("ghost@example.com");
        failed.EntityType.Should().BeEmpty("no entity is involved in an auth event");
    }

    [Fact]
    public async Task Filtering_by_entity_type_excludes_authentication_events()
    {
        IAuditLogWriter writer = new AuditLogWriter(_db, TimeProvider.System);
        await writer.WriteAuthEventAsync(AuditAction.Login, "7", "ana@example.com", null);
        await WriteTransactionAsync("7", nameof(Service));

        var result = await _repository.GetPagedAsync(new AuditLogQueryParams(EntityType: nameof(Service)));

        result.TotalCount.Should().Be(1);
        result.Items.Single().EntityType.Should().Be(nameof(Service));
    }
}

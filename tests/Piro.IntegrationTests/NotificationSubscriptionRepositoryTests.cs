using Microsoft.EntityFrameworkCore;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Persistence;
using Piro.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>
/// The update path copies fields onto the tracked entity one by one, so a field omitted from that list
/// silently keeps its old value however the caller sets it. That is exactly how a cleared tag filter
/// survived an update, so these run against a real Postgres rather than trusting the assignment list.
/// </summary>
public class NotificationSubscriptionRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private PiroDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<PiroDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        _db = new PiroDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_NullFilterJson_ClearsAStoredTagFilter()
    {
        var repo = new NotificationSubscriptionRepository(_db);
        var created = await repo.CreateAsync(NewSubscription(filterJson: """{"all":[{"key":"env","value":"prod"}]}"""));

        // The admin removed the tag filter, so the update states the whole subscription with no filter.
        var cleared = await repo.UpdateAsync(WithFilter(created, filterJson: null));

        Assert.Null(cleared.FilterJson);
        // Re-read rather than trusting the returned instance: the bug was in what got persisted.
        Assert.Null((await repo.GetByIdAsync(created.Id))!.FilterJson);
    }

    [Fact]
    public async Task UpdateAsync_NewFilterJson_ReplacesTheStoredOne()
    {
        var repo = new NotificationSubscriptionRepository(_db);
        var created = await repo.CreateAsync(NewSubscription(filterJson: """{"all":[{"key":"env","value":"prod"}]}"""));

        const string replacement = """{"any":[{"key":"team","value":"payments"}]}""";
        await repo.UpdateAsync(WithFilter(created, replacement));

        Assert.Equal(replacement, (await repo.GetByIdAsync(created.Id))!.FilterJson);
    }

    [Fact]
    public async Task UpdateAsync_AddingAFilterWhereThereWasNone_PersistsIt()
    {
        var repo = new NotificationSubscriptionRepository(_db);
        var created = await repo.CreateAsync(NewSubscription(filterJson: null));

        const string added = """{"all":[{"key":"env","value":"staging"}]}""";
        await repo.UpdateAsync(WithFilter(created, added));

        Assert.Equal(added, (await repo.GetByIdAsync(created.Id))!.FilterJson);
    }

    private static NotificationSubscription NewSubscription(string? filterJson) => new()
    {
        Name = "asda",
        EventsJson = """["alert:created","alert:resolved"]""",
        MinSeverity = AlertSeverity.Warning,
        TargetKind = NotificationTargetKind.Channel,
        Enabled = true,
        FilterJson = filterJson,
    };

    /// <summary>
    /// Mirrors what the app service does on update: build a fresh entity carrying the whole desired
    /// state, rather than mutating the tracked one.
    /// </summary>
    private static NotificationSubscription WithFilter(NotificationSubscription source, string? filterJson) => new()
    {
        Id = source.Id,
        Name = source.Name,
        EventsJson = source.EventsJson,
        MinSeverity = source.MinSeverity,
        TargetKind = source.TargetKind,
        UserId = source.UserId,
        IntegrationId = source.IntegrationId,
        Target = source.Target,
        Enabled = source.Enabled,
        FilterJson = filterJson,
    };
}

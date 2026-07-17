using Microsoft.EntityFrameworkCore;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Persistence;
using Piro.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>
/// Verifies dashboard alert-volume aggregation against a real Postgres instance — specifically that
/// volume counts distinct Alert instances and is never inflated by <see cref="Alert.OccurrenceCount"/>.
/// </summary>
public class MetricsRepositoryTests : IAsyncLifetime
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
    public async Task GetDashboardMetrics_AlertVolume_CountsInstancesNotOccurrences()
    {
        var repo = new MetricsRepository(_db);
        var from = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        // One alert that folded 99 repeated evaluations into a single row, plus two ordinary alerts.
        // Volume must be 3 (distinct rows), NOT 101 (summed occurrences).
        _db.Alerts.AddRange(
            new Alert { FiredAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero), OccurrenceCount = 99, MessageFingerprint = "a" },
            new Alert { FiredAt = new DateTimeOffset(2026, 7, 10, 13, 0, 0, TimeSpan.Zero), OccurrenceCount = 1, MessageFingerprint = "b" },
            new Alert { FiredAt = new DateTimeOffset(2026, 7, 11, 9, 0, 0, TimeSpan.Zero), OccurrenceCount = 1, MessageFingerprint = "c" });
        await _db.SaveChangesAsync();

        var metrics = await repo.GetDashboardMetricsAsync(from, to);

        Assert.Equal(3, metrics.AlertMetrics.AlertCount);

        var daily = metrics.AlertMetrics.DailyAlertCounts.ToList();
        // Two alerts on 07-10 (one with OccurrenceCount 99), one on 07-11.
        Assert.Equal(2, daily.Single(d => d.Date == new DateOnly(2026, 7, 10)).Count);
        Assert.Equal(1, daily.Single(d => d.Date == new DateOnly(2026, 7, 11)).Count);
        Assert.Equal(3, daily.Sum(d => d.Count));
    }
}

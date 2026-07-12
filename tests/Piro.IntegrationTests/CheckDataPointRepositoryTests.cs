using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Persistence;
using Piro.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>Verifies duplicate data point inserts are handled against a real Postgres instance.</summary>
public class CheckDataPointRepositoryTests : IAsyncLifetime
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
    public async Task CreateAsync_DuplicateCheckMinuteRegion_ReturnsFalseWithoutThrowing()
    {
        var repo = new CheckDataPointRepository(_db, NullLogger<CheckDataPointRepository>.Instance);
        var (serviceId, checkId) = await SeedServiceAndCheckAsync();

        const long timestamp = 1_700_000_000;
        var first = new CheckDataPoint
        {
            CheckId = checkId, Timestamp = timestamp, Status = ServiceStatus.UP,
            DataType = DataPointType.REALTIME, WorkerRegion = "default"
        };
        var duplicate = new CheckDataPoint
        {
            CheckId = checkId, Timestamp = timestamp, Status = ServiceStatus.DOWN,
            DataType = DataPointType.REALTIME, WorkerRegion = "default"
        };

        var firstResult = await repo.CreateAsync(first);
        var duplicateResult = await repo.CreateAsync(duplicate);

        Assert.True(firstResult);
        Assert.False(duplicateResult);

        var stored = await _db.CheckDataPoints.SingleAsync(p => p.CheckId == checkId && p.Timestamp == timestamp);
        Assert.Equal(ServiceStatus.UP, stored.Status); // duplicate write did not overwrite the original row
    }

    [Fact]
    public async Task CreateAsync_DifferentRegionSameMinute_BothPersist()
    {
        var repo = new CheckDataPointRepository(_db, NullLogger<CheckDataPointRepository>.Instance);
        var (_, checkId) = await SeedServiceAndCheckAsync();

        const long timestamp = 1_700_000_060;
        var pointA = new CheckDataPoint
        {
            CheckId = checkId, Timestamp = timestamp, Status = ServiceStatus.UP,
            DataType = DataPointType.REALTIME, WorkerRegion = "us-east"
        };
        var pointB = new CheckDataPoint
        {
            CheckId = checkId, Timestamp = timestamp, Status = ServiceStatus.UP,
            DataType = DataPointType.REALTIME, WorkerRegion = "eu-west"
        };

        Assert.True(await repo.CreateAsync(pointA));
        Assert.True(await repo.CreateAsync(pointB));

        var count = await _db.CheckDataPoints.CountAsync(p => p.CheckId == checkId && p.Timestamp == timestamp);
        Assert.Equal(2, count);
    }

    private async Task<(int ServiceId, int CheckId)> SeedServiceAndCheckAsync()
    {
        var service = new Service { Slug = $"svc-{Guid.NewGuid():N}", Name = "Test Service" };
        _db.Services.Add(service);
        await _db.SaveChangesAsync();

        var check = new Check
        {
            ServiceId = service.Id, Slug = "check-1", Name = "Check 1",
            Type = CheckType.HTTP, TypeDataJson = "{}"
        };
        _db.Checks.Add(check);
        await _db.SaveChangesAsync();

        return (service.Id, check.Id);
    }
}

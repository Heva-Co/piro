using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Piro.Domain.Entities;
using Piro.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Piro.IntegrationTests;

/// <summary>
/// Covers the reentrant unit of work (RFC 0019 §4.3): nested Begin calls join the outermost
/// transaction so the config reconciler can compose the application services — each of which wraps
/// itself in a transaction — and still have the whole document commit or roll back as one.
/// </summary>
public class UnitOfWorkNestingTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private PiroDbContext _db = null!;
    private UnitOfWork _uow = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        _db = NewContext();
        await _db.Database.MigrateAsync();
        _uow = new UnitOfWork(_db);
    }

    public async Task DisposeAsync()
    {
        await _uow.DisposeAsync();
        await _db.DisposeAsync();
        await _container.DisposeAsync();
    }

    private PiroDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PiroDbContext>().UseNpgsql(_connectionString).Options);

    private static Service NewService(string slug) => new() { Slug = slug, Name = slug };

    /// <summary>Reads through a separate connection, so only committed rows are visible.</summary>
    private async Task<bool> ExistsAsync(string slug)
    {
        await using var verify = NewContext();
        return await verify.Services.AnyAsync(s => s.Slug == slug);
    }

    [Fact]
    public async Task InnerCommit_DoesNotEndTheOuterTransaction()
    {
        const string slug = "nest-inner-commit";

        await _uow.BeginAsync();
        await _uow.BeginAsync();
        _db.Services.Add(NewService(slug));
        await _db.SaveChangesAsync();
        await _uow.CommitAsync();   // inner: must not commit

        // Still inside the outer transaction, so rolling back now must discard the row.
        await _uow.RollbackAsync();

        (await ExistsAsync(slug)).Should().BeFalse();
    }

    [Fact]
    public async Task OuterCommit_PersistsWorkDoneByInnerScopes()
    {
        const string first = "nest-a";
        const string second = "nest-b";

        await _uow.BeginAsync();

        await _uow.BeginAsync();
        _db.Services.Add(NewService(first));
        await _db.SaveChangesAsync();
        await _uow.CommitAsync();

        await _uow.BeginAsync();
        _db.Services.Add(NewService(second));
        await _db.SaveChangesAsync();
        await _uow.CommitAsync();

        await _uow.CommitAsync();   // outer: the only real commit

        (await ExistsAsync(first)).Should().BeTrue();
        (await ExistsAsync(second)).Should().BeTrue();
    }

    [Fact]
    public async Task InnerRollback_DiscardsTheWholeDocument()
    {
        // The guarantee that makes an apply all-or-nothing: one failed resource undoes the rest.
        const string slug = "nest-rollback";

        await _uow.BeginAsync();

        await _uow.BeginAsync();
        _db.Services.Add(NewService(slug));
        await _db.SaveChangesAsync();
        await _uow.CommitAsync();

        await _uow.BeginAsync();
        await _uow.RollbackAsync();   // inner failure

        await _uow.RollbackAsync();   // outer unwinds

        (await ExistsAsync(slug)).Should().BeFalse();
    }

    [Fact]
    public async Task CommittingAfterAnInnerRollback_Throws()
    {
        // An outer scope that swallowed an inner failure must not be able to persist a partial write.
        const string slug = "nest-abort";

        await _uow.BeginAsync();
        _db.Services.Add(NewService(slug));
        await _db.SaveChangesAsync();

        await _uow.BeginAsync();
        await _uow.RollbackAsync();

        var act = () => _uow.CommitAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*rolled back*");
        (await ExistsAsync(slug)).Should().BeFalse();
    }

    [Fact]
    public async Task SingleLevelUse_IsUnchanged()
    {
        // The existing CheckAppService path must behave exactly as it did before nesting existed.
        const string slug = "nest-single";

        await _uow.BeginAsync();
        _db.Services.Add(NewService(slug));
        await _db.SaveChangesAsync();
        await _uow.CommitAsync();

        (await ExistsAsync(slug)).Should().BeTrue();
    }

    [Fact]
    public async Task CommitWithoutBegin_Throws()
    {
        var act = () => _uow.CommitAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

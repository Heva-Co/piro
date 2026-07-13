using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IWorkerRegistrationRepository"/>.</summary>
internal class WorkerRegistrationRepository(PiroDbContext db) : IWorkerRegistrationRepository
{
    public async Task<WorkerRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.WorkerRegistrations.FindAsync([id], ct);

    public async Task<WorkerRegistration?> FindByWorkerTokenHashAsync(string hash, CancellationToken ct = default) =>
        await db.WorkerRegistrations
            .FirstOrDefaultAsync(w => w.WorkerTokenHash == hash && w.IsActive, ct);

    public async Task<IEnumerable<WorkerRegistration>> GetAllAsync(CancellationToken ct = default) =>
        await db.WorkerRegistrations.OrderBy(w => w.CreatedAt).ToListAsync(ct);

    public async Task<WorkerRegistration> CreateAsync(WorkerRegistration worker, CancellationToken ct = default)
    {
        db.WorkerRegistrations.Add(worker);
        await db.SaveChangesAsync(ct);
        return worker;
    }

    public async Task<WorkerRegistration> UpdateAsync(WorkerRegistration worker, CancellationToken ct = default)
    {
        db.WorkerRegistrations.Update(worker);
        await db.SaveChangesAsync(ct);
        return worker;
    }

    public async Task DeleteAsync(WorkerRegistration worker, CancellationToken ct = default)
    {
        db.WorkerRegistrations.Remove(worker);
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearDefaultAsync(CancellationToken ct = default)
    {
        await db.WorkerRegistrations
            .Where(w => w.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.IsDefault, false), ct);
    }

    public async Task SetAsDefaultAsync(WorkerRegistration worker, CancellationToken ct = default)
    {
        // Reuse the caller's transaction if one is already open — Npgsql/EF Core doesn't allow
        // nesting BeginTransactionAsync on the same connection (see SiteConfigRepository.SetManyAsync).
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        try
        {
            await db.WorkerRegistrations
                .Where(w => w.IsDefault && w.Id != worker.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.IsDefault, false), ct);

            worker.IsDefault = true;
            db.WorkerRegistrations.Update(worker);
            await db.SaveChangesAsync(ct);

            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }
}

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
}

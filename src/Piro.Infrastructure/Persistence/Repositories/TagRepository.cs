using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ITagRepository"/> (RFC 0008, Part A).</summary>
internal class TagRepository(PiroDbContext db) : ITagRepository
{
    public async Task<Tag?> GetTagByKeyAsync(string key, CancellationToken ct = default) =>
        await db.Tags.FirstOrDefaultAsync(t => t.Key == key, ct);

    public async Task<Tag> GetOrCreateTagAsync(string key, TagSource source, CancellationToken ct = default)
    {
        var existing = await db.Tags.FirstOrDefaultAsync(t => t.Key == key, ct);
        if (existing is not null) return existing;

        var tag = new Tag { Key = key, Source = source };
        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);
        return tag;
    }

    public Task<bool> ServiceExistsAsync(int serviceId, CancellationToken ct = default) =>
        db.Services.AnyAsync(s => s.Id == serviceId, ct);

    public Task<bool> CheckExistsAsync(int checkId, CancellationToken ct = default) =>
        db.Checks.AnyAsync(c => c.Id == checkId, ct);

    public Task<bool> WorkerExistsAsync(Guid workerId, CancellationToken ct = default) =>
        db.WorkerRegistrations.AnyAsync(w => w.Id == workerId, ct);

    public async Task<IReadOnlyList<ServiceTag>> GetServiceTagsAsync(int serviceId, CancellationToken ct = default) =>
        await db.ServiceTags.Include(st => st.Tag).Where(st => st.ServiceId == serviceId).ToListAsync(ct);

    public async Task<IReadOnlyList<CheckTag>> GetCheckTagsAsync(int checkId, CancellationToken ct = default) =>
        await db.CheckTags.Include(ct2 => ct2.Tag).Where(ct2 => ct2.CheckId == checkId).ToListAsync(ct);

    public async Task<IReadOnlyList<WorkerTag>> GetWorkerTagsAsync(Guid workerId, CancellationToken ct = default) =>
        await db.WorkerTags.Include(wt => wt.Tag).Where(wt => wt.WorkerRegistrationId == workerId).ToListAsync(ct);

    public async Task<int?> GetParentServiceIdAsync(int checkId, CancellationToken ct = default)
    {
        var row = await db.Checks.Where(c => c.Id == checkId)
            .Select(c => new { c.ServiceId })
            .FirstOrDefaultAsync(ct);
        return row?.ServiceId;
    }

    public async Task ReplaceServiceUserTagsAsync(int serviceId, IReadOnlyList<(Tag Tag, string? Value)> tags, CancellationToken ct = default)
    {
        var existing = await db.ServiceTags
            .Where(st => st.ServiceId == serviceId && st.Tag.Source == TagSource.User)
            .ToListAsync(ct);
        db.ServiceTags.RemoveRange(existing);
        foreach (var (tag, value) in tags)
            db.ServiceTags.Add(new ServiceTag { ServiceId = serviceId, TagId = tag.Id, Value = value });
        await db.SaveChangesAsync(ct);
    }

    public async Task ReplaceCheckUserTagsAsync(int checkId, IReadOnlyList<(Tag Tag, string? Value)> tags, CancellationToken ct = default)
    {
        var existing = await db.CheckTags
            .Where(ct2 => ct2.CheckId == checkId && ct2.Tag.Source == TagSource.User)
            .ToListAsync(ct);
        db.CheckTags.RemoveRange(existing);
        foreach (var (tag, value) in tags)
            db.CheckTags.Add(new CheckTag { CheckId = checkId, TagId = tag.Id, Value = value });
        await db.SaveChangesAsync(ct);
    }

    public async Task ReplaceWorkerUserTagsAsync(Guid workerId, IReadOnlyList<(Tag Tag, string? Value)> tags, CancellationToken ct = default)
    {
        var existing = await db.WorkerTags
            .Where(wt => wt.WorkerRegistrationId == workerId && wt.Tag.Source == TagSource.User)
            .ToListAsync(ct);
        db.WorkerTags.RemoveRange(existing);
        foreach (var (tag, value) in tags)
            db.WorkerTags.Add(new WorkerTag { WorkerRegistrationId = workerId, TagId = tag.Id, Value = value });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetUserKeysAsync(string? prefix, CancellationToken ct = default)
    {
        var q = db.Tags.Where(t => t.Source == TagSource.User);
        if (!string.IsNullOrWhiteSpace(prefix))
            q = q.Where(t => t.Key.StartsWith(prefix));
        return await q.Select(t => t.Key).OrderBy(k => k).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetValuesForKeyAsync(string key, CancellationToken ct = default)
    {
        var serviceValues = db.ServiceTags.Where(st => st.Tag.Key == key && st.Value != null).Select(st => st.Value!);
        var checkValues = db.CheckTags.Where(ct2 => ct2.Tag.Key == key && ct2.Value != null).Select(ct2 => ct2.Value!);
        var workerValues = db.WorkerTags.Where(wt => wt.Tag.Key == key && wt.Value != null).Select(wt => wt.Value!);
        return await serviceValues.Union(checkValues).Union(workerValues).OrderBy(v => v).ToListAsync(ct);
    }
}

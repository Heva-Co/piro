using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Piro.Application.Interfaces;
using Piro.Domain.Auditing;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Auditing;

/// <summary>
/// Records user-initiated changes to <see cref="IAuditable"/> entities into <see cref="AuditLog"/>
/// (issue #17).
/// </summary>
/// <remarks>
/// Runs in two passes, because neither half of the information is available at a single moment.
/// Before the save, the change tracker still holds the original values and the entries' states;
/// after it, newly inserted rows finally have their database-generated keys. So
/// <see cref="SavingChangesAsync"/> captures states and value snapshots, and
/// <see cref="SavedChangesAsync"/> resolves the ids and writes the entries.
/// <para>
/// Writing those entries is itself a <c>SaveChanges</c>, which would re-enter this interceptor, so
/// a reentrancy flag suppresses the second pass. The audit write is not audited.
/// </para>
/// <para>
/// Nothing is recorded when <see cref="ICurrentUserAccessor"/> resolves no user. Background jobs,
/// seeding and migrations therefore leave no trace even when they touch a marked entity — the trail
/// is a record of what people did, and a row attributed to "system" would only be noise. Lifting
/// that restriction later is a matter of removing one early return, with no schema change.
/// </para>
/// </remarks>
internal class AuditSaveChangesInterceptor(
    ICurrentUserAccessor currentUserAccessor,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    /// <summary>
    /// Property names never captured in a snapshot, matched case-insensitively. These are the
    /// sensitive members inherited from ASP.NET Core Identity, which
    /// <see cref="NotAuditedAttribute"/> cannot reach because they are declared on the base class.
    /// </summary>
    private static readonly HashSet<string> AlwaysExcludedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "TwoFactorSecret",
    };

    /// <summary>
    /// Property names preferred, in order, as an entry's human-readable label.
    /// </summary>
    private static readonly string[] LabelPropertyNames = ["Slug", "Name", "Title", "Key", "Email"];

    /// <summary>
    /// Set while this interceptor's own audit write is in flight, so the resulting
    /// <c>SaveChanges</c> is not itself audited. Scoped per DbContext, like the interceptor.
    /// </summary>
    private bool isWritingAuditEntries;

    /// <summary>Entries captured before the save, awaiting their entity ids.</summary>
    private readonly List<PendingAuditEntry> pending = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (pending.Count == 0 || eventData.Context is null)
            return await base.SavedChangesAsync(eventData, result, cancellationToken);

        var entries = BuildEntries();

        isWritingAuditEntries = true;
        try
        {
            eventData.Context.Set<AuditLog>().AddRange(entries);
            await eventData.Context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            isWritingAuditEntries = false;
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (pending.Count == 0 || eventData.Context is null)
            return base.SavedChanges(eventData, result);

        var entries = BuildEntries();

        isWritingAuditEntries = true;
        try
        {
            eventData.Context.Set<AuditLog>().AddRange(entries);
            eventData.Context.SaveChanges();
        }
        finally
        {
            isWritingAuditEntries = false;
        }

        return base.SavedChanges(eventData, result);
    }

    /// <summary>
    /// Clears anything captured for a save that threw, so a failed transaction cannot leak its
    /// entries into whatever the same context does next.
    /// </summary>
    public override void SaveChangesFailed(DbContextErrorEventData eventData) => pending.Clear();

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        pending.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Snapshots every audited change in the change tracker. Runs before the save, while original
    /// values and entity states are still intact.
    /// </summary>
    private void CaptureChanges(DbContext? context)
    {
        pending.Clear();

        if (context is null || isWritingAuditEntries)
            return;

        var user = currentUserAccessor.Current;
        if (user is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not IAuditable)
                continue;

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Create,
                EntityState.Modified => AuditAction.Update,
                EntityState.Deleted => AuditAction.Delete,
                _ => (AuditAction?)null,
            };

            if (action is null)
                continue;

            // An "update" that changed no audited property is not worth a row — it would otherwise
            // record touching an entity whose secret-only field moved, with two identical snapshots.
            if (action == AuditAction.Update && !HasAuditedModification(entry))
                continue;

            pending.Add(new PendingAuditEntry(
                entry,
                action.Value,
                OldValues: action == AuditAction.Create ? null : Snapshot(entry, original: true),
                NewValues: action == AuditAction.Delete ? null : Snapshot(entry, original: false),
                Label: ResolveLabel(entry)));
        }
    }

    /// <summary>
    /// Turns the captured snapshots into rows. Runs after the save, so entity ids are final.
    /// </summary>
    private List<AuditLog> BuildEntries()
    {
        var user = currentUserAccessor.Current!;

        // UUIDv7 rather than NewGuid for index locality: values written close in time stay close in
        // the B-tree. It is not a reliable sort key on its own, since its time component only has
        // millisecond precision — the feed orders by CreatedAt instead.
        var correlationId = Guid.CreateVersion7();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // The transaction is named after a root entity in preference to a join row: saving a Service
        // and three of its tags should read as "edited Service", not as one of the tag rows.
        var primary = pending.FirstOrDefault(p => !IsJoinEntity(p.Entry)) ?? pending[0];

        var entries = new List<AuditLog>(pending.Count);
        foreach (var item in pending)
        {
            entries.Add(new AuditLog
            {
                CorrelationId = correlationId,
                IsPrimary = ReferenceEquals(item, primary),
                UserId = user.UserId,
                UserEmail = user.Email,
                Action = item.Action,
                EntityType = item.Entry.Metadata.ClrType.Name,
                EntityId = ResolveEntityId(item.Entry),
                EntityLabel = item.Label,
                OldValues = item.OldValues,
                NewValues = item.NewValues,
                IpAddress = user.IpAddress,
                CreatedAt = now,
            });
        }

        pending.Clear();
        return entries;
    }

    /// <summary>True when the change touched at least one property that is actually audited.</summary>
    private static bool HasAuditedModification(EntityEntry entry) =>
        entry.Properties.Any(p => p.IsModified && IsAudited(p));

    /// <summary>
    /// Serialises the entity's audited scalar properties. Navigation properties and collections are
    /// never included: a related entity produces its own entry, sharing the correlation id.
    /// </summary>
    private static string Snapshot(EntityEntry entry, bool original)
    {
        var values = new Dictionary<string, object?>();
        foreach (var property in entry.Properties)
        {
            if (!IsAudited(property))
                continue;

            // Original values are unavailable for an Added entity, and meaningless there anyway.
            values[property.Metadata.Name] = original && entry.State != EntityState.Added
                ? property.OriginalValue
                : property.CurrentValue;
        }

        return JsonSerializer.Serialize(values);
    }

    /// <summary>
    /// Whether a property belongs in a snapshot: a real scalar, not a key, and not excluded either
    /// by <see cref="NotAuditedAttribute"/> or by the inherited-secrets deny list.
    /// </summary>
    private static bool IsAudited(PropertyEntry property)
    {
        var metadata = property.Metadata;

        // The key is already recorded as EntityId; repeating it in the diff adds nothing.
        if (metadata.IsPrimaryKey())
            return false;

        // Shadow properties have no CLR member to annotate and are EF bookkeeping, not user data.
        if (metadata.IsShadowProperty())
            return false;

        if (AlwaysExcludedProperties.Contains(metadata.Name))
            return false;

        return metadata.PropertyInfo?.IsDefined(typeof(NotAuditedAttribute), inherit: true) != true;
    }

    /// <summary>
    /// Stringifies the entity's primary key. Keys in this model are variously int, Guid and string,
    /// and join entities have composite ones, so parts are joined with '|'.
    /// </summary>
    private static string ResolveEntityId(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProperties is null || keyProperties.Count == 0)
            return string.Empty;

        var parts = keyProperties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? string.Empty);

        return string.Join('|', parts);
    }

    /// <summary>
    /// Picks a human-readable label so the feed can name the affected row without resolving ids.
    /// Returns null when the entity has no suitable property, which is typical of join entities.
    /// </summary>
    private static string? ResolveLabel(EntityEntry entry)
    {
        foreach (var name in LabelPropertyNames)
        {
            var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == name);
            if (property?.CurrentValue is string value && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    /// <summary>
    /// Heuristic for "this row only links two other rows": a composite primary key made entirely of
    /// foreign keys. Used only to prefer a root entity when naming the transaction.
    /// </summary>
    private static bool IsJoinEntity(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProperties is null || keyProperties.Count < 2)
            return false;

        return keyProperties.All(p => p.IsForeignKey());
    }

    /// <summary>A change captured before the save, still awaiting the entity's final id.</summary>
    private record PendingAuditEntry(
        EntityEntry Entry,
        AuditAction Action,
        string? OldValues,
        string? NewValues,
        string? Label);
}

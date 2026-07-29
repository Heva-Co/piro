using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="AuditLog"/> table (issue #17).</summary>
internal class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Action).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.UserId).HasMaxLength(64).IsRequired();
        builder.Property(l => l.UserEmail).HasMaxLength(256).IsRequired();
        builder.Property(l => l.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(l => l.EntityId).HasMaxLength(256).IsRequired();
        builder.Property(l => l.EntityLabel).HasMaxLength(512);

        // Room for an IPv6 address with an interface suffix.
        builder.Property(l => l.IpAddress).HasMaxLength(64);

        // No length cap: a snapshot is as wide as the entity's audited scalars.
        builder.Property(l => l.OldValues).HasColumnType("text");
        builder.Property(l => l.NewValues).HasColumnType("text");

        // The listing paginates over transactions, not rows, ordering by CorrelationId DESC. That
        // works because the interceptor assigns UUIDv7, whose ordering is already chronological —
        // so the feed's default sort is an index scan with no aggregate over CreatedAt. This same
        // index then serves the lookup of every entry sharing a group.
        builder.HasIndex(l => l.CorrelationId);

        // "What happened to this Service?" — the history of one specific row.
        builder.HasIndex(l => new { l.EntityType, l.EntityId });

        // "What did this user do?" — no FK on UserId, deliberately: the trail has to survive the
        // account being deleted, which is exactly when it matters most. UserEmail is denormalised
        // on the row for the same reason.
        builder.HasIndex(l => l.UserId);

        // Date-range filtering, and the fallback ordering for any query that is not group-paginated.
        builder.HasIndex(l => l.CreatedAt);
    }
}

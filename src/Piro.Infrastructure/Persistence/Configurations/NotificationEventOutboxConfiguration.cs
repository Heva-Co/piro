using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="NotificationEventOutbox"/> table (RFC 0009 §5).</summary>
internal class NotificationEventOutboxConfiguration : IEntityTypeConfiguration<NotificationEventOutbox>
{
    public void Configure(EntityTypeBuilder<NotificationEventOutbox> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.EventType).HasMaxLength(128).IsRequired();
        builder.Property(o => o.OrderingKey).HasMaxLength(128).IsRequired();
        builder.Property(o => o.PayloadJson).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);

        // Drain query: pick up Pending/Processing rows whose NextAttemptAt has arrived.
        builder.HasIndex(o => new { o.Status, o.NextAttemptAt });
        // Ordering check: within an OrderingKey, is there an earlier-id row not yet terminal?
        builder.HasIndex(o => new { o.OrderingKey, o.Id });
    }
}

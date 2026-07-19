using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="NotificationDeliveryLog"/> table (RFC 0009 §5).</summary>
internal class NotificationDeliveryLogConfiguration : IEntityTypeConfiguration<NotificationDeliveryLog>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.IdempotencyKey).HasMaxLength(256).IsRequired();
        builder.Property(l => l.EventType).HasMaxLength(128).IsRequired();
        builder.Property(l => l.TargetKind).HasMaxLength(32).IsRequired();
        builder.Property(l => l.TargetDescriptor).HasMaxLength(256).IsRequired();
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(16);

        // The idempotency guarantee: a duplicate (event × destination) delivery cannot be recorded twice.
        builder.HasIndex(l => l.IdempotencyKey).IsUnique();
    }
}

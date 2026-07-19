using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="NotificationSubscription"/> table (RFC 0009 §4.4, §5).</summary>
internal class NotificationSubscriptionConfiguration : IEntityTypeConfiguration<NotificationSubscription>
{
    public void Configure(EntityTypeBuilder<NotificationSubscription> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.EventsJson).IsRequired();
        builder.Property(s => s.MinSeverity).HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.TargetKind).HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.Target).HasMaxLength(256);

        builder.HasIndex(s => s.Enabled);

        // Personal destination — a user. Restrict so a referenced user can't be silently orphaned.
        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Channel/Integration destination — delete the subscription if its integration is removed.
        builder.HasOne(s => s.Integration)
            .WithMany()
            .HasForeignKey(s => s.IntegrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

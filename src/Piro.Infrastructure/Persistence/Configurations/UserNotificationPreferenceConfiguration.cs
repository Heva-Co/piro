using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.ToTable("UserNotificationPreferences");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Handle).HasMaxLength(500).IsRequired();
        // Unique per user + instance + handle — a user can't duplicate the same destination, but two
        // different users may share the same integration instance (a shared Telegram bot, say).
        builder.HasIndex(p => new { p.UserId, p.IntegrationInstanceId, p.Handle }).IsUnique();
        builder.HasIndex(p => p.UserId);
        builder.HasOne(p => p.User)
            .WithMany(u => u.NotificationPreferences)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.Integration)
            .WithMany()
            .HasForeignKey(p => p.IntegrationInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

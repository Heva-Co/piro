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
        builder.HasIndex(p => new { p.UserId, p.IntegrationId }).IsUnique();
        builder.HasIndex(p => p.UserId);
        builder.HasOne(p => p.User)
            .WithMany(u => u.NotificationPreferences)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.Integration)
            .WithMany()
            .HasForeignKey(p => p.IntegrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

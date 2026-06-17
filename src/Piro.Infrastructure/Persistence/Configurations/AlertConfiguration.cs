using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="AlertConfig"/> and <see cref="AlertConfigNotificationChannel"/>.</summary>
internal class AlertConfigConfiguration : IEntityTypeConfiguration<AlertConfig>
{
    public void Configure(EntityTypeBuilder<AlertConfig> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.CheckId);

        builder.Property(a => a.AlertFor).HasConversion<string>();
        builder.Property(a => a.AlertValue).HasMaxLength(255).IsRequired();
        builder.Property(a => a.Severity).HasConversion<string>().HasDefaultValue(AlertSeverity.Warning);

        builder.HasOne(a => a.Check)
            .WithMany(c => c.AlertConfigs)
            .HasForeignKey(a => a.CheckId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal class AlertConfigNotificationChannelConfiguration : IEntityTypeConfiguration<AlertConfigNotificationChannel>
{
    public void Configure(EntityTypeBuilder<AlertConfigNotificationChannel> builder)
    {
        builder.ToTable("AlertConfigNotificationChannels");
        builder.HasKey(ac => new { ac.AlertConfigId, ac.NotificationChannelId });

        builder.HasOne(ac => ac.AlertConfig)
            .WithMany(a => a.AlertConfigNotificationChannels)
            .HasForeignKey(ac => ac.AlertConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ac => ac.NotificationChannel)
            .WithMany(c => c.AlertConfigNotificationChannels)
            .HasForeignKey(ac => ac.NotificationChannelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

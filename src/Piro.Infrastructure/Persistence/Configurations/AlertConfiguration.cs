using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="AlertConfig"/> and <see cref="AlertConfigTrigger"/>.</summary>
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

internal class AlertConfigTriggerConfiguration : IEntityTypeConfiguration<AlertConfigTrigger>
{
    public void Configure(EntityTypeBuilder<AlertConfigTrigger> builder)
    {
        builder.HasKey(at => new { at.AlertConfigId, at.TriggerId });

        builder.HasOne(at => at.AlertConfig)
            .WithMany(a => a.AlertConfigTriggers)
            .HasForeignKey(at => at.AlertConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(at => at.Trigger)
            .WithMany(t => t.AlertConfigTriggers)
            .HasForeignKey(at => at.TriggerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

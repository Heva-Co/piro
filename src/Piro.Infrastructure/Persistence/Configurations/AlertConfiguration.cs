using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="AlertConfig"/>.</summary>
internal class AlertConfigConfiguration : IEntityTypeConfiguration<AlertConfig>
{
    public void Configure(EntityTypeBuilder<AlertConfig> builder)
    {
        builder.HasKey(a => a.Id);

        // Restricted to one AlertConfig per Check for now — multi-config evaluation is deferred.
        builder.HasIndex(a => a.CheckId).IsUnique();

        builder.Property(a => a.AlertFor).HasConversion<string>();
        builder.Property(a => a.AlertValue).HasMaxLength(255).IsRequired();
        builder.Property(a => a.Severity).HasConversion<string>().HasDefaultValue(AlertSeverity.Warning);

        builder.HasOne(a => a.Check)
            .WithMany(c => c.AlertConfigs)
            .HasForeignKey(a => a.CheckId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

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

        // A Check can have multiple AlertConfigs (e.g. a Warning at 30 days-to-expiry and a
        // Critical at 7, on the same SSL check) — see RFC 0002. Non-unique index, kept for lookup.
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

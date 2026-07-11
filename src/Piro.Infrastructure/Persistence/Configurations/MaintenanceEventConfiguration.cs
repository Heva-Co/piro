using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="MaintenanceEvent"/> entity.</summary>
internal class MaintenanceEventConfiguration : IEntityTypeConfiguration<MaintenanceEvent>
{
    public void Configure(EntityTypeBuilder<MaintenanceEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.MaintenanceId, e.StartDateTime }).IsUnique();
        builder.HasIndex(e => e.MaintenanceId);
        builder.HasIndex(e => e.StartDateTime);
        builder.HasIndex(e => e.EndDateTime);
        builder.HasIndex(e => e.Status);

        builder.Property(e => e.Status).HasConversion<string>().HasDefaultValue(MaintenanceEventStatus.Scheduled);

        builder.HasOne(e => e.Maintenance)
            .WithMany(m => m.Events)
            .HasForeignKey(e => e.MaintenanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

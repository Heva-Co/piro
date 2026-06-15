using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="MaintenanceService"/> junction.</summary>
internal class MaintenanceServiceConfiguration : IEntityTypeConfiguration<MaintenanceService>
{
    public void Configure(EntityTypeBuilder<MaintenanceService> builder)
    {
        builder.HasKey(ms => new { ms.MaintenanceId, ms.ServiceId });

        builder.Property(ms => ms.Impact).HasConversion<string>();

        builder.HasOne(ms => ms.Maintenance)
            .WithMany(m => m.MaintenanceServices)
            .HasForeignKey(ms => ms.MaintenanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ms => ms.Service)
            .WithMany(s => s.MaintenanceServices)
            .HasForeignKey(ms => ms.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

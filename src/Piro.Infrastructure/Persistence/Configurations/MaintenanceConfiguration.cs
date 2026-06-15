using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Maintenance"/> entity.</summary>
internal class MaintenanceConfiguration : IEntityTypeConfiguration<Maintenance>
{
    public void Configure(EntityTypeBuilder<Maintenance> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title).HasMaxLength(255).IsRequired();
        builder.Property(m => m.RRule).HasMaxLength(500).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasDefaultValue(MaintenanceStatus.Active);
    }
}

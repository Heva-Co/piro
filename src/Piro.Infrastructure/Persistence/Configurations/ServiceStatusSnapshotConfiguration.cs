using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="ServiceStatusSnapshot"/> cache table.</summary>
internal class ServiceStatusSnapshotConfiguration : IEntityTypeConfiguration<ServiceStatusSnapshot>
{
    public void Configure(EntityTypeBuilder<ServiceStatusSnapshot> builder)
    {
        builder.HasKey(s => new { s.ServiceId, s.Timestamp });

        builder.HasIndex(s => new { s.ServiceId, s.Timestamp });

        builder.Property(s => s.ComputedStatus).HasConversion<string>();

        builder.HasOne(s => s.Service)
            .WithMany()
            .HasForeignKey(s => s.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

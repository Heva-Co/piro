using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="CheckDataPoint"/> time-series table.</summary>
internal class CheckDataPointConfiguration : IEntityTypeConfiguration<CheckDataPoint>
{
    public void Configure(EntityTypeBuilder<CheckDataPoint> builder)
    {
        // WorkerRegion is part of the PK so multi-region checks can store one
        // data point per worker per minute for the same check.
        builder.HasKey(d => new { d.CheckId, d.Timestamp, d.WorkerRegion });

        builder.HasIndex(d => new { d.CheckId, d.Timestamp });

        builder.Property(d => d.WorkerRegion).HasMaxLength(100).HasDefaultValue("default");

        builder.Property(d => d.Status).HasConversion<string>();
        builder.Property(d => d.DataType).HasMaxLength(30);

        builder.HasOne(d => d.Check)
            .WithMany(c => c.DataPoints)
            .HasForeignKey(d => d.CheckId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

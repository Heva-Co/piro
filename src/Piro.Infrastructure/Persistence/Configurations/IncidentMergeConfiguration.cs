using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="IncidentMerge"/> table.</summary>
internal class IncidentMergeConfiguration : IEntityTypeConfiguration<IncidentMerge>
{
    public void Configure(EntityTypeBuilder<IncidentMerge> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Reason).HasMaxLength(255);

        builder.HasOne(m => m.SourceIncident)
            .WithMany(i => i.MergesAsSource)
            .HasForeignKey(m => m.SourceIncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.TargetIncident)
            .WithMany(i => i.MergesAsTarget)
            .HasForeignKey(m => m.TargetIncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

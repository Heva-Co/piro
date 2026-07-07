using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class IncidentImpactChangeConfiguration : IEntityTypeConfiguration<IncidentImpactChange>
{
    public void Configure(EntityTypeBuilder<IncidentImpactChange> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => new { c.IncidentId, c.Timestamp });
        builder.Property(c => c.Impact).HasConversion<string>().IsRequired();
        builder.HasOne(c => c.Incident)
            .WithMany(i => i.ImpactChanges)
            .HasForeignKey(c => c.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

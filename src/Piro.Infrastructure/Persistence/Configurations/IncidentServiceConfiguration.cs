using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="IncidentService"/> junction.</summary>
internal class IncidentServiceConfiguration : IEntityTypeConfiguration<IncidentService>
{
    public void Configure(EntityTypeBuilder<IncidentService> builder)
    {
        builder.HasKey(i => new { i.IncidentId, i.ServiceId });

        builder.Property(i => i.Impact).HasConversion<string>();

        builder.HasOne(i => i.Incident)
            .WithMany(inc => inc.IncidentServices)
            .HasForeignKey(i => i.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Service)
            .WithMany(s => s.IncidentServices)
            .HasForeignKey(i => i.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.TriggeringCheck)
            .WithMany()
            .HasForeignKey(i => i.TriggeringCheckId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Incident"/> entity.</summary>
internal class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.StartDateTime);

        builder.Property(i => i.Title).HasMaxLength(255).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasDefaultValue(IncidentStatus.Investigating);
        builder.Property(i => i.Source).HasMaxLength(30);
        builder.Property(i => i.CurrentImpact).HasConversion<string>().HasSentinel(ServiceStatus.NO_DATA).HasDefaultValue(ServiceStatus.DOWN);
        builder.Property(i => i.Visibility).HasConversion<string>().HasDefaultValue(IncidentVisibility.Private);
        builder.Ignore(i => i.IsPublic);
    }
}

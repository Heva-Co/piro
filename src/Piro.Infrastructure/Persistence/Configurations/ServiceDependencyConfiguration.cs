using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="ServiceDependency"/> DAG edge.</summary>
internal class ServiceDependencyConfiguration : IEntityTypeConfiguration<ServiceDependency>
{
    public void Configure(EntityTypeBuilder<ServiceDependency> builder)
    {
        builder.HasKey(d => new { d.ServiceId, d.DependsOnServiceId });

        builder.HasIndex(d => d.DependsOnServiceId);

        builder.Property(d => d.PropagationMode)
            .HasConversion<string>()
            .HasDefaultValue(DependencyPropagationMode.Blocking);
    }
}

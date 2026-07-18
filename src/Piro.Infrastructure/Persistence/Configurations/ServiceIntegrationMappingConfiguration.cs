using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class ServiceIntegrationMappingConfiguration : IEntityTypeConfiguration<ServiceIntegrationMapping>
{
    public void Configure(EntityTypeBuilder<ServiceIntegrationMapping> builder)
    {
        builder.ToTable("ServiceIntegrationMappings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MappingJson).HasColumnType("jsonb").HasDefaultValue("{}");

        // One mapping per (service, integration) pairing.
        builder.HasIndex(x => new { x.ServiceId, x.IntegrationId }).IsUnique();

        builder.HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Integration)
            .WithMany()
            .HasForeignKey(x => x.IntegrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

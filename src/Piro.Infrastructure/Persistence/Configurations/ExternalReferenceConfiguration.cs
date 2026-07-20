using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class ExternalReferenceConfiguration : IEntityTypeConfiguration<ExternalReference>
{
    public void Configure(EntityTypeBuilder<ExternalReference> builder)
    {
        builder.ToTable("ExternalReferences");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActionId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ExternalId).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(512);

        // Provider-specific coordinates, opaque to Piro (RFC 0012 §4.5) — same shape as ServiceIntegrationMapping.MappingJson.
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb").HasDefaultValue("{}");

        // Polymorphic target pointer — deliberately NOT a FK (no common base among Alert/Incident/Maintenance).
        // Non-unique: a target may accumulate references from several integrations. Serves the "links for this object" read.
        builder.HasIndex(x => new { x.TargetType, x.TargetId });

        // Real FK to Integration; deleting the integration drops its references, consistent with its other relations.
        builder.HasOne(x => x.Integration)
            .WithMany()
            .HasForeignKey(x => x.IntegrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

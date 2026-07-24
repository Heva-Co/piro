using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="ServiceTag"/> junction (RFC 0008).</summary>
internal class ServiceTagConfiguration : IEntityTypeConfiguration<ServiceTag>
{
    public void Configure(EntityTypeBuilder<ServiceTag> builder)
    {
        builder.HasKey(st => new { st.ServiceId, st.TagId });

        builder.HasIndex(st => st.ServiceId);
        builder.HasIndex(st => st.TagId);

        builder.Property(st => st.Value).HasMaxLength(TagConstants.MaxValueLength);

        builder.HasOne(st => st.Service)
            .WithMany(s => s.ServiceTags)
            .HasForeignKey(st => st.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(st => st.Tag)
            .WithMany(t => t.ServiceTags)
            .HasForeignKey(st => st.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Tag"/> key catalog (RFC 0008).</summary>
internal class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Key).HasMaxLength(TagConstants.MaxKeyLength).IsRequired();
        builder.HasIndex(t => t.Key).IsUnique();

        builder.Property(t => t.Source).HasConversion<string>().IsRequired();
    }
}

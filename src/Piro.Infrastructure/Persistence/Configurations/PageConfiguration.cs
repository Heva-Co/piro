using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Page"/> entity.</summary>
internal class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.Path).IsUnique();

        builder.Property(p => p.Path).HasMaxLength(255).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(255).IsRequired();
        builder.Property(p => p.SettingsJson).HasColumnType("jsonb");
    }
}

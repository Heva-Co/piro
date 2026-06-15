using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class PiroLogConfiguration : IEntityTypeConfiguration<PiroLog>
{
    public void Configure(EntityTypeBuilder<PiroLog> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Level).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Message).IsRequired();
        builder.Property(l => l.SourceContext).HasMaxLength(500);
        builder.HasIndex(l => l.Timestamp);
        builder.HasIndex(l => l.Level);
    }
}

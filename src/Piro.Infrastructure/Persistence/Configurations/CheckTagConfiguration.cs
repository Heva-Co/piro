using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="CheckTag"/> junction (RFC 0008).</summary>
internal class CheckTagConfiguration : IEntityTypeConfiguration<CheckTag>
{
    public void Configure(EntityTypeBuilder<CheckTag> builder)
    {
        builder.HasKey(ct => new { ct.CheckId, ct.TagId });

        builder.HasIndex(ct => ct.CheckId);
        builder.HasIndex(ct => ct.TagId);

        builder.Property(ct => ct.Value).HasMaxLength(TagConstants.MaxValueLength);

        builder.HasOne(ct => ct.Check)
            .WithMany(c => c.CheckTags)
            .HasForeignKey(ct => ct.CheckId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ct => ct.Tag)
            .WithMany(t => t.CheckTags)
            .HasForeignKey(ct => ct.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

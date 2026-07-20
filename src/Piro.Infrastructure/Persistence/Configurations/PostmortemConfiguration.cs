using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Postmortem"/> report aggregate (RFC 0005).</summary>
internal class PostmortemConfiguration : IEntityTypeConfiguration<Postmortem>
{
    public void Configure(EntityTypeBuilder<Postmortem> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.Status);

        builder.Property(p => p.Name).HasMaxLength(255).IsRequired();
        // No DB default: the app always sets Status on create; a DB default with no sentinel can't
        // distinguish an explicit Draft from an unset value.
        builder.Property(p => p.Status).HasConversion<string>();
        builder.Property(p => p.ReviewOwnerName).HasMaxLength(255);

        // Review owner: nullable FK, ON DELETE SET NULL — deleting the user preserves the report and
        // its ReviewOwnerName snapshot (RFC 0005 §4.7).
        builder.HasOne(p => p.ReviewOwner)
            .WithMany()
            .HasForeignKey(p => p.ReviewOwnerUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

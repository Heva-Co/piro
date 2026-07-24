using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="CheckRequiredWorkerTag"/> junction (RFC 0008 Part B).</summary>
internal class CheckRequiredWorkerTagConfiguration : IEntityTypeConfiguration<CheckRequiredWorkerTag>
{
    public void Configure(EntityTypeBuilder<CheckRequiredWorkerTag> builder)
    {
        builder.HasKey(rt => new { rt.CheckId, rt.TagId });

        builder.HasIndex(rt => rt.CheckId);
        builder.HasIndex(rt => rt.TagId);

        builder.Property(rt => rt.Value).HasMaxLength(TagConstants.MaxValueLength);

        builder.HasOne(rt => rt.Check)
            .WithMany(c => c.RequiredWorkerTags)
            .HasForeignKey(rt => rt.CheckId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rt => rt.Tag)
            .WithMany(t => t.CheckRequiredWorkerTags)
            .HasForeignKey(rt => rt.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

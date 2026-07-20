using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for author-owned postmortem timeline annotations (<see cref="PostmortemTimelineEntry"/>, RFC 0005 §4.4).</summary>
internal class PostmortemTimelineEntryConfiguration : IEntityTypeConfiguration<PostmortemTimelineEntry>
{
    public void Configure(EntityTypeBuilder<PostmortemTimelineEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PostmortemId);

        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.AuthorName).HasMaxLength(255);

        builder.HasOne(e => e.Postmortem)
            .WithMany(p => p.TimelineEntries)
            .HasForeignKey(e => e.PostmortemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

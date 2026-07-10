using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class IncidentCommentConfiguration : IEntityTypeConfiguration<IncidentComment>
{
    public void Configure(EntityTypeBuilder<IncidentComment> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.IncidentId);

        builder.Property(c => c.Status).HasConversion<string>();
        builder.Property(c => c.Visibility)
            .HasConversion<string>()
            .HasDefaultValue(CommentVisibility.Private);

        builder.HasOne(c => c.Incident)
            .WithMany(i => i.Comments)
            .HasForeignKey(c => c.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

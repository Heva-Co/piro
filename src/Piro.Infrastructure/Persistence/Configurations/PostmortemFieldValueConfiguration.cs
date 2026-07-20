using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for a report's per-section analysis content (<see cref="PostmortemFieldValue"/>, RFC 0005 §4.3).</summary>
internal class PostmortemFieldValueConfiguration : IEntityTypeConfiguration<PostmortemFieldValue>
{
    public void Configure(EntityTypeBuilder<PostmortemFieldValue> builder)
    {
        builder.HasKey(v => v.Id);
        builder.HasIndex(v => new { v.PostmortemId, v.FieldDefinitionId }).IsUnique();

        builder.Property(v => v.Value).IsRequired().HasDefaultValue("");

        builder.HasOne(v => v.Postmortem)
            .WithMany(p => p.FieldValues)
            .HasForeignKey(v => v.PostmortemId)
            .OnDelete(DeleteBehavior.Cascade);

        // A definition in use can't be hard-deleted — deactivate via IsActive instead (RFC 0005 §4.3).
        builder.HasOne(v => v.FieldDefinition)
            .WithMany()
            .HasForeignKey(v => v.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

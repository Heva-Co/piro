using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for the analysis template (<see cref="PostmortemFieldDefinition"/>). Seeds the eight
/// standard sections via <see cref="RelationalEntityTypeBuilderExtensions"/> <c>HasData</c> so the
/// template ships with the migration and is present on every instance (RFC 0005 §4.3).
/// </summary>
internal class PostmortemFieldDefinitionConfiguration : IEntityTypeConfiguration<PostmortemFieldDefinition>
{
    public void Configure(EntityTypeBuilder<PostmortemFieldDefinition> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => d.Key).IsUnique();

        builder.Property(d => d.Key).HasMaxLength(60).IsRequired();
        builder.Property(d => d.Heading).HasMaxLength(120).IsRequired();
        // No DB default: FieldType's CLR default (Text) isn't the desired default, and a DB default with
        // no sentinel would silently override an explicitly-set Text field. The app/seed always sets it.
        builder.Property(d => d.FieldType).HasConversion<string>();
        builder.Property(d => d.IsActive).HasDefaultValue(true);

        builder.HasData(
            Seed(1, "overview", "Overview", "A high-level summary of what happened, for a general audience.", 0),
            Seed(2, "what_happened", "What Happened", "A detailed, chronological account of the incident.", 1),
            Seed(3, "resolution", "Resolution", "How the incident was ultimately resolved.", 2),
            Seed(4, "root_causes", "Root Causes", "The conditions that allowed the incident to happen. Aim for the underlying causes, not just the trigger.", 3),
            Seed(5, "impact", "Impact", "Who and what was affected, and to what degree.", 4),
            Seed(6, "what_went_well", "What Went Well?", "Things that worked as intended during detection and response.", 5),
            Seed(7, "what_didnt", "What Didn't Go So Well?", "Things that hindered detection or response and should be improved.", 6),
            Seed(8, "action_items", "Action Items", "Concrete follow-up work, each with an accountable owner.", 7)
        );
    }

    private static PostmortemFieldDefinition Seed(int id, string key, string heading, string helpText, int sortOrder) => new()
    {
        Id = id,
        Key = key,
        Heading = heading,
        HelpText = helpText,
        FieldType = PostmortemFieldType.LongText,
        SortOrder = sortOrder,
        IsActive = true,
        IsSystem = true,
    };
}

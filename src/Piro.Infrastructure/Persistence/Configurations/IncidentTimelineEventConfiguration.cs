using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class IncidentTimelineEventConfiguration : IEntityTypeConfiguration<IncidentTimelineEvent>
{
    public void Configure(EntityTypeBuilder<IncidentTimelineEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.IncidentId, e.OccurredAt });

        builder.Property(e => e.Type).HasConversion<string>();
        builder.Property(e => e.OldStatus).HasConversion<string>();
        builder.Property(e => e.NewStatus).HasConversion<string>();
        builder.Property(e => e.Visibility)
            .HasConversion<string>()
            .HasDefaultValue(EventVisibility.Private);

        builder.HasOne(e => e.Incident)
            .WithMany(i => i.TimelineEvents)
            .HasForeignKey(e => e.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

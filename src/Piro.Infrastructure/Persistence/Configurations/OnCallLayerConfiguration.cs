using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class OnCallLayerConfiguration : IEntityTypeConfiguration<OnCallLayer>
{
    public void Configure(EntityTypeBuilder<OnCallLayer> builder)
    {
        builder.ToTable("OnCallLayers");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.RecurrenceRule).HasMaxLength(500).IsRequired();
        builder.HasIndex(l => new { l.ScheduleId, l.Order }).IsUnique();
        builder.HasOne(l => l.Schedule)
            .WithMany(s => s.Layers)
            .HasForeignKey(l => l.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

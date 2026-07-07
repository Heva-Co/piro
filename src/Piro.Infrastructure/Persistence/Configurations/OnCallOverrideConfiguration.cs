using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class OnCallOverrideConfiguration : IEntityTypeConfiguration<OnCallOverride>
{
    public void Configure(EntityTypeBuilder<OnCallOverride> builder)
    {
        builder.ToTable("OnCallOverrides");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Reason).HasMaxLength(500);
        builder.HasIndex(o => new { o.ScheduleId, o.StartsAtUtc, o.EndsAtUtc });
        builder.HasOne(o => o.Schedule)
            .WithMany(s => s.Overrides)
            .HasForeignKey(o => o.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.ReplacesUser)
            .WithMany()
            .HasForeignKey(o => o.ReplacesUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}

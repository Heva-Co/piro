using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class EscalationStepConfiguration : IEntityTypeConfiguration<EscalationStep>
{
    public void Configure(EntityTypeBuilder<EscalationStep> b)
    {
        b.ToTable("EscalationSteps");
        b.HasKey(s => s.Id);
        b.HasIndex(s => new { s.PolicyId, s.Order }).IsUnique();

        // DB-level defaults so existing steps backfill to today's fire-once behavior (RFC 0006 §5).
        b.Property(s => s.MaxRetries).HasDefaultValue(1);
        b.Property(s => s.RetryIntervalMinutes).HasDefaultValue(0);
        b.HasOne(s => s.Schedule)
            .WithMany()
            .HasForeignKey(s => s.ScheduleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

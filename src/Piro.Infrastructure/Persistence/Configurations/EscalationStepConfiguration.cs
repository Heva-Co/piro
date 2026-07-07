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
        b.HasOne(s => s.Schedule)
            .WithMany()
            .HasForeignKey(s => s.ScheduleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

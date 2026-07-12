using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class EscalationPolicyConfiguration : IEntityTypeConfiguration<EscalationPolicy>
{
    public void Configure(EntityTypeBuilder<EscalationPolicy> b)
    {
        b.ToTable("EscalationPolicies");
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.Description).HasMaxLength(1000);
        b.HasIndex(p => p.Name).IsUnique();
        b.HasMany(p => p.Steps)
            .WithOne(s => s.Policy)
            .HasForeignKey(s => s.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

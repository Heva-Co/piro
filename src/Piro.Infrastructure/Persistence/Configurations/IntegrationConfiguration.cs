using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Integration"/> entity.</summary>
internal class IntegrationConfiguration : IEntityTypeConfiguration<Integration>
{
    public void Configure(EntityTypeBuilder<Integration> builder)
    {
        builder.ToTable("Integrations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(i => i.Name).HasMaxLength(255).IsRequired();
        builder.Property(i => i.ConfigJson).HasColumnType("jsonb").HasDefaultValue("{}");

        builder.HasMany(i => i.Checks)
            .WithOne(c => c.Integration)
            .HasForeignKey(c => c.IntegrationId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(i => i.EscalationPolicy)
            .WithMany(p => p.Integrations)
            .HasForeignKey(i => i.EscalationPolicyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

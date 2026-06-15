using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="WorkerRegistration"/> entity.</summary>
internal class WorkerRegistrationConfiguration : IEntityTypeConfiguration<WorkerRegistration>
{
    public void Configure(EntityTypeBuilder<WorkerRegistration> builder)
    {
        builder.HasKey(w => w.Id);
        builder.HasIndex(w => w.WorkerTokenHash).IsUnique();

        builder.Property(w => w.Name).HasMaxLength(255).IsRequired();
        builder.Property(w => w.Region).HasMaxLength(100).HasDefaultValue("default");
        builder.Property(w => w.WorkerTokenHash).HasMaxLength(64).IsRequired();
    }
}

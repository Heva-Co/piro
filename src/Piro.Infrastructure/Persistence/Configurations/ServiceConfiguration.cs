using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Service"/> entity.</summary>
internal class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.Slug).IsUnique();

        builder.Property(s => s.Slug).HasMaxLength(255).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(255).IsRequired();
        builder.Property(s => s.ImageUrl).HasMaxLength(500);
        builder.Property(s => s.CurrentStatus).HasConversion<string>().HasDefaultValue(ServiceStatus.NO_DATA);
        builder.Property(s => s.DefaultStatus).HasConversion<string>().HasDefaultValue(ServiceStatus.NO_DATA);

        builder.HasMany(s => s.DependsOn)
            .WithOne(d => d.Service)
            .HasForeignKey(d => d.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.DependedOnBy)
            .WithOne(d => d.DependsOnService)
            .HasForeignKey(d => d.DependsOnServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

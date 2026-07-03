using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Check"/> entity.</summary>
internal class CheckConfiguration : IEntityTypeConfiguration<Check>
{
    public void Configure(EntityTypeBuilder<Check> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => new { c.ServiceId, c.Slug }).IsUnique();
        builder.HasIndex(c => c.IsActive);

        builder.Property(c => c.Slug).HasMaxLength(255).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(255).IsRequired();
        builder.Property(c => c.Type).HasConversion<string>();
        builder.Property(c => c.Cron).HasMaxLength(255).HasDefaultValue("* * * * *");
        builder.Property(c => c.TypeDataJson).HasColumnType("jsonb").HasDefaultValue("{}");
        builder.Property(c => c.CurrentStatus).HasConversion<string>().HasDefaultValue(ServiceStatus.NO_DATA);
        builder.Property(c => c.DefaultStatus).HasConversion<string>().HasDefaultValue(ServiceStatus.NO_DATA);
        builder.Property(c => c.Criticality).HasConversion<string>().HasDefaultValue(CheckCriticality.High);

        builder.HasOne(c => c.Service)
            .WithMany(s => s.Checks)
            .HasForeignKey(c => c.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

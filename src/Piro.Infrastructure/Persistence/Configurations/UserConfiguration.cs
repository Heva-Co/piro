using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for extra properties on <see cref="AppUser"/> beyond the Identity base.</summary>
internal class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.Name).HasMaxLength(255).IsRequired();
        builder.Property(u => u.ExternalId).HasMaxLength(500);
        builder.Property(u => u.ExternalProvider).HasMaxLength(50);
        builder.Property(u => u.TimeZone).HasMaxLength(100).HasDefaultValue("UTC");
    }
}

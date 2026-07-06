using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class OnCallLayerUserConfiguration : IEntityTypeConfiguration<OnCallLayerUser>
{
    public void Configure(EntityTypeBuilder<OnCallLayerUser> builder)
    {
        builder.ToTable("OnCallLayerUsers");
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => new { u.LayerId, u.Position });
        builder.HasOne(u => u.Layer)
            .WithMany(l => l.Users)
            .HasForeignKey(u => u.LayerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(u => u.User)
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

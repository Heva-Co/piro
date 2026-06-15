using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="PageService"/> junction.</summary>
internal class PageServiceConfiguration : IEntityTypeConfiguration<PageService>
{
    public void Configure(EntityTypeBuilder<PageService> builder)
    {
        builder.HasKey(ps => new { ps.PageId, ps.ServiceId });

        builder.HasIndex(ps => ps.PageId);
        builder.HasIndex(ps => ps.ServiceId);

        builder.Property(ps => ps.SettingsJson).HasColumnType("jsonb");

        builder.HasOne(ps => ps.Page)
            .WithMany(p => p.PageServices)
            .HasForeignKey(ps => ps.PageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ps => ps.Service)
            .WithMany(s => s.PageServices)
            .HasForeignKey(ps => ps.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

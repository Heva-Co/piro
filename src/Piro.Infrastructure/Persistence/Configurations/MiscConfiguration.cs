using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mappings for <see cref="NotificationChannel"/>, <see cref="ApiKey"/>, and <see cref="SiteData"/>.</summary>
internal class NotificationChannelConfiguration : IEntityTypeConfiguration<NotificationChannel>
{
    public void Configure(EntityTypeBuilder<NotificationChannel> builder)
    {
        builder.ToTable("NotificationChannels");
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(255).IsRequired();
        builder.Property(c => c.Type).HasConversion<string>();
        builder.Property(c => c.MetaJson).HasColumnType("jsonb").HasDefaultValue("{}");
        builder.Property(c => c.IsGlobal).HasDefaultValue(false);
        builder.Property(c => c.IsLocked).HasDefaultValue(false);
    }
}

internal class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.HasKey(k => k.Id);
        builder.HasIndex(k => k.HashedKey).IsUnique();
        builder.HasIndex(k => k.Name).IsUnique();
        builder.Property(k => k.Name).HasMaxLength(255).IsRequired();
        builder.Property(k => k.HashedKey).HasMaxLength(255).IsRequired();
        builder.Property(k => k.MaskedKey).HasMaxLength(255).IsRequired();
        builder.Property(k => k.Status).HasMaxLength(20).HasDefaultValue("ACTIVE");

        builder.HasOne(k => k.User)
            .WithMany()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}

internal class SiteDataConfiguration : IEntityTypeConfiguration<SiteData>
{
    public void Configure(EntityTypeBuilder<SiteData> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.Key).IsUnique();
        builder.Property(s => s.Key).HasMaxLength(255).IsRequired();
        builder.Property(s => s.DataType).HasMaxLength(50).HasDefaultValue("string");
    }
}

internal class IncidentCommentConfiguration : IEntityTypeConfiguration<IncidentComment>
{
    public void Configure(EntityTypeBuilder<IncidentComment> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.IncidentId);

        builder.Property(c => c.State).HasConversion<string>();
        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasDefaultValue(IncidentStatus.Active);

        builder.HasOne(c => c.Incident)
            .WithMany(i => i.Comments)
            .HasForeignKey(c => c.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

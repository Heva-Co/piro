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
        builder.Property(c => c.IsInactive).HasDefaultValue(false);
        builder.HasOne(c => c.Integration)
               .WithMany()
               .HasForeignKey(c => c.IntegrationId)
               .OnDelete(DeleteBehavior.SetNull);
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
        builder.Property(k => k.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(ApiKeyStatus.Active);

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

internal class IntegrationConfiguration : IEntityTypeConfiguration<Integration>
{
    public void Configure(EntityTypeBuilder<Integration> builder)
    {
        builder.ToTable("Integrations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Name).HasMaxLength(255).IsRequired();
        builder.Property(i => i.Type).HasConversion<string>();
        builder.Property(i => i.ConfigJson).HasColumnType("jsonb").HasDefaultValue("{}");

        builder.HasMany(i => i.Checks)
            .WithOne(c => c.Integration)
            .HasForeignKey(c => c.IntegrationId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}

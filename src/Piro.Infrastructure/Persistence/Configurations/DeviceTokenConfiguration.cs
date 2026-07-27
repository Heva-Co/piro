using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("DeviceTokens");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Token).HasMaxLength(4000).IsRequired();
        builder.Property(d => d.DeviceName).HasMaxLength(200);
        // base64url of a 65-byte EC point is 87 chars; 128 leaves room without inviting junk.
        builder.Property(d => d.PushPublicKey).HasMaxLength(128);
        builder.Property(d => d.Platform).HasConversion<string>().HasMaxLength(20);

        // A device token is unique per user; re-registering the same token upserts the existing row.
        builder.HasIndex(d => new { d.UserId, d.Token }).IsUnique();
        builder.HasIndex(d => d.UserId);

        builder.HasOne(d => d.User)
            .WithMany(u => u.DeviceTokens)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

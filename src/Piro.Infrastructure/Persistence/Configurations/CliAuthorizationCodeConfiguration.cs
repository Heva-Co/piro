using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class CliAuthorizationCodeConfiguration : IEntityTypeConfiguration<CliAuthorizationCode>
{
    public void Configure(EntityTypeBuilder<CliAuthorizationCode> builder)
    {
        builder.ToTable("CliAuthorizationCodes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CodeHash).HasMaxLength(128).IsRequired();
        builder.Property(c => c.RedirectUri).HasMaxLength(500).IsRequired();
        builder.Property(c => c.CodeChallenge).HasMaxLength(128).IsRequired();
        builder.Property(c => c.State).HasMaxLength(128).IsRequired();
        builder.Property(c => c.ClientLabel).HasMaxLength(200);

        // Exchange looks the code up by hash, so the index is what makes redemption a point lookup.
        builder.HasIndex(c => c.CodeHash).IsUnique();

        // Expired rows are swept in bulk; the index keeps that from scanning the table.
        builder.HasIndex(c => c.ExpiresAt);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

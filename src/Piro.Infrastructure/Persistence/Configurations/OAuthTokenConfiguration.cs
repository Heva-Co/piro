using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class OAuthTokenConfiguration : IEntityTypeConfiguration<OAuthToken>
{
    public void Configure(EntityTypeBuilder<OAuthToken> builder)
    {
        builder.ToTable("OAuthTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        // Ciphertext — length is generous since IDataProtector output is larger than the plaintext.
        builder.Property(x => x.AccessToken).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.RefreshToken).HasMaxLength(4000);
        builder.Property(x => x.Scopes).HasMaxLength(500);

        // One token set per integration.
        builder.HasIndex(x => x.IntegrationId).IsUnique();

        builder.HasOne(x => x.Integration)
            .WithMany()
            .HasForeignKey(x => x.IntegrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

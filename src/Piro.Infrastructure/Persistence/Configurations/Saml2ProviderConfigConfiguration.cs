using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class Saml2ProviderConfigConfiguration : IEntityTypeConfiguration<Saml2ProviderConfig>
{
    public void Configure(EntityTypeBuilder<Saml2ProviderConfig> builder)
    {
        builder.ToTable("Saml2ProviderConfigs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(50);
        builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IdpEntityId).HasMaxLength(500).IsRequired();
        builder.Property(x => x.IdpSsoUrl).HasMaxLength(500).IsRequired();
        // PEM/base64 X.509 certs run large; allow generous room.
        builder.Property(x => x.IdpSigningCertificate).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.SpEntityId).HasMaxLength(500);
        builder.Property(x => x.AllowedDomains).HasMaxLength(1000);
        builder.Property(x => x.DefaultRole).HasMaxLength(50).IsRequired();
    }
}

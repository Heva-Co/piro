using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class OidcProviderConfigConfiguration : IEntityTypeConfiguration<OidcProviderConfig>
{
    public void Configure(EntityTypeBuilder<OidcProviderConfig> builder)
    {
        builder.ToTable("OidcProviderConfigs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(50);
        builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Authority).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ClientId).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ClientSecretProtected).IsRequired();
        builder.Property(x => x.RedirectUri).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Scopes).HasMaxLength(500).IsRequired();
        builder.Property(x => x.AllowedDomains).HasMaxLength(1000);
        builder.Property(x => x.DefaultRole).HasMaxLength(50).IsRequired();
    }
}

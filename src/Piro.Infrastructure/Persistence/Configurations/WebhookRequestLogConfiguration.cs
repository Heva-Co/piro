using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="WebhookRequestLog"/> entity.</summary>
internal class WebhookRequestLogConfiguration : IEntityTypeConfiguration<WebhookRequestLog>
{
    public void Configure(EntityTypeBuilder<WebhookRequestLog> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.RawPayload).HasColumnType("jsonb").IsRequired();
        builder.Property(l => l.Outcome).HasConversion<string>();

        builder.HasIndex(l => new { l.IntegrationId, l.ReceivedAt });

        builder.HasOne(l => l.Integration)
            .WithMany()
            .HasForeignKey(l => l.IntegrationId)
            .OnDelete(DeleteBehavior.Cascade);

        // The Alert ↔ WebhookRequestLog relationship (Alert.SourceRequestLogId owns the FK) is
        // configured on the Alert side — see AlertEntityConfiguration.
    }
}

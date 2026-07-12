using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="EscalationDeliveryLog"/> table.</summary>
internal class EscalationDeliveryLogConfiguration : IEntityTypeConfiguration<EscalationDeliveryLog>
{
    public void Configure(EntityTypeBuilder<EscalationDeliveryLog> builder)
    {
        builder.HasKey(l => l.Id);
        builder.HasIndex(l => l.AlertId);

        builder.Property(l => l.UserName).HasMaxLength(255).IsRequired();
        builder.Property(l => l.ChannelType).HasConversion<string>();

        builder.HasOne(l => l.Alert)
            .WithMany(a => a.EscalationDeliveryLogs)
            .HasForeignKey(l => l.AlertId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

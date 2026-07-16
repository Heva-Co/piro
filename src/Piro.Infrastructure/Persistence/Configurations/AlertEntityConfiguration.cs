using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Alert"/> entity.</summary>
internal class AlertEntityConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.HasKey(a => a.Id);
        // Sentinel = NO_DATA (the CLR default for ServiceStatus): without it, EF can't tell "field never
        // set" from "explicitly set to NO_DATA", so it always uses the DB default on insert regardless.
        builder.Property(a => a.ImpactAtFireTime).HasConversion<string>().HasSentinel(ServiceStatus.NO_DATA).HasDefaultValue(ServiceStatus.DOWN);
        builder.Property(a => a.MessageFingerprint).HasMaxLength(512).IsRequired();
        builder.Property(a => a.Source).HasConversion<string>().HasDefaultValue(AlertSource.Internal);
        builder.Property(a => a.ExternalId).HasMaxLength(255);

        builder.HasIndex(a => new { a.AlertConfigId, a.ResolvedAt });
        builder.HasIndex(a => a.IncidentId);
        builder.HasIndex(a => new { a.ServiceId, a.ResolvedAt });
        builder.HasIndex(a => a.EscalationPolicyId);
        // Dedup key for webhook-sourced alerts (RFC 0001 §4.8) — (Source, ExternalId), not GCP-specific.
        builder.HasIndex(a => new { a.Source, a.ExternalId });

        // Nullable — a webhook-sourced alert (RFC 0001) has no internal AlertConfig behind it.
        builder.HasOne(a => a.AlertConfig)
            .WithMany()
            .HasForeignKey(a => a.AlertConfigId)
            .OnDelete(DeleteBehavior.SetNull);

        // Nullable — an orphan alert (RFC 0001 §4.2) has no Check/Service to correlate against.
        builder.HasOne(a => a.Check)
            .WithMany()
            .HasForeignKey(a => a.CheckId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Service)
            .WithMany()
            .HasForeignKey(a => a.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Incident)
            .WithMany(i => i.Alerts)
            .HasForeignKey(a => a.IncidentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.EscalationPolicy)
            .WithMany()
            .HasForeignKey(a => a.EscalationPolicyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.SourceRequestLog)
            .WithOne(l => l.Alert)
            .HasForeignKey<Alert>(a => a.SourceRequestLogId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

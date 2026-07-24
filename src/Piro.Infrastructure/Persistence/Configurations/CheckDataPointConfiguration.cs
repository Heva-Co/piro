using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="CheckDataPoint"/> time-series table.</summary>
internal class CheckDataPointConfiguration : IEntityTypeConfiguration<CheckDataPoint>
{
    public void Configure(EntityTypeBuilder<CheckDataPoint> builder)
    {
        // WorkerRegion is part of the PK so multi-region checks can store one
        // data point per worker per minute for the same check.
        builder.HasKey(d => new { d.CheckId, d.Timestamp, d.WorkerRegion });

        builder.HasIndex(d => new { d.CheckId, d.Timestamp });

        builder.Property(d => d.WorkerRegion).HasMaxLength(100).HasDefaultValue("default");

        builder.Property(d => d.Status).HasConversion<string>();
        builder.Property(d => d.DataType).HasConversion<string>().HasMaxLength(30);

        // Every numeric measurement the check reported, as a jsonb object keyed by dimension name.
        // Serialized to/from the entity's Dictionary; queried by key for latency aggregations and alerts.
        builder.Property(d => d.Dimensions)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, double>>(v, (JsonSerializerOptions?)null) ?? new(),
                new DimensionsComparer());

        builder.HasOne(d => d.Check)
            .WithMany(c => c.DataPoints)
            .HasForeignKey(d => d.CheckId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Value comparer so EF Core detects changes to the <see cref="CheckDataPoint.Dimensions"/> dictionary
/// (a mutable reference type mapped to jsonb) by content rather than by reference.
/// </summary>
internal sealed class DimensionsComparer()
    : Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, double>>(
        (a, b) => a != null && b != null && a.Count == b.Count && !a.Except(b).Any(),
        v => v.Aggregate(0, (acc, kv) => HashCode.Combine(acc, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
        v => new Dictionary<string, double>(v));

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="WorkerTag"/> junction (RFC 0008).</summary>
internal class WorkerTagConfiguration : IEntityTypeConfiguration<WorkerTag>
{
    public void Configure(EntityTypeBuilder<WorkerTag> builder)
    {
        builder.HasKey(wt => new { wt.WorkerRegistrationId, wt.TagId });

        builder.HasIndex(wt => wt.WorkerRegistrationId);
        builder.HasIndex(wt => wt.TagId);

        builder.Property(wt => wt.Value).HasMaxLength(TagConstants.MaxValueLength);

        builder.HasOne(wt => wt.Worker)
            .WithMany(w => w.WorkerTags)
            .HasForeignKey(wt => wt.WorkerRegistrationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(wt => wt.Tag)
            .WithMany(t => t.WorkerTags)
            .HasForeignKey(wt => wt.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

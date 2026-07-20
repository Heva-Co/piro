using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="PostmortemIncident"/> N:M junction (RFC 0005 §4.6), modeled on IncidentService.</summary>
internal class PostmortemIncidentConfiguration : IEntityTypeConfiguration<PostmortemIncident>
{
    public void Configure(EntityTypeBuilder<PostmortemIncident> builder)
    {
        builder.HasKey(pi => new { pi.PostmortemId, pi.IncidentId });

        builder.HasOne(pi => pi.Postmortem)
            .WithMany(p => p.PostmortemIncidents)
            .HasForeignKey(pi => pi.PostmortemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pi => pi.Incident)
            .WithMany(i => i.PostmortemIncidents)
            .HasForeignKey(pi => pi.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

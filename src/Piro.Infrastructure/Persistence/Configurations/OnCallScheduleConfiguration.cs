using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Configurations;

internal class OnCallScheduleConfiguration : IEntityTypeConfiguration<OnCallSchedule>
{
    public void Configure(EntityTypeBuilder<OnCallSchedule> builder)
    {
        builder.ToTable("OnCallSchedules");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(1000);
        builder.Property(s => s.TimeZone).HasMaxLength(100).HasDefaultValue("UTC");
    }
}

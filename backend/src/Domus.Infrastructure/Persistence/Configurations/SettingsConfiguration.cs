using Domus.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domus.Infrastructure.Persistence.Configurations;

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.OccurredAt).IsRequired();
        builder.Property(a => a.ActorName).HasMaxLength(60).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(60).IsRequired();
        builder.Property(a => a.Details).HasMaxLength(1000);

        builder.HasIndex(a => a.OccurredAt).IsDescending();
    }
}

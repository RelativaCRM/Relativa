using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public sealed class AuditProcessedEventConfiguration : IEntityTypeConfiguration<AuditProcessedEvent>
{
    public void Configure(EntityTypeBuilder<AuditProcessedEvent> builder)
    {
        builder.ToTable("audit_processed_event");
        builder.HasKey(x => x.EventId);

        builder.Property(x => x.EventId)
            .HasColumnName("event_id");
        builder.Property(x => x.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}

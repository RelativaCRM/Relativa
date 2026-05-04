using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public sealed class AuditOutboxMessageConfiguration : IEntityTypeConfiguration<AuditOutboxMessage>
{
    public void Configure(EntityTypeBuilder<AuditOutboxMessage> builder)
    {
        builder.ToTable("audit_outbox");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.EventId)
            .HasColumnName("event_id")
            .IsRequired();
        builder.Property(x => x.PayloadJson)
            .HasColumnName("payload_json")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(x => x.RoutingKey)
            .HasColumnName("routing_key")
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(x => x.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();
        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();
        builder.Property(x => x.PublishedAtUtc)
            .HasColumnName("published_at_utc")
            .HasColumnType("timestamptz");
        builder.Property(x => x.PublishAttempts)
            .HasColumnName("publish_attempts")
            .IsRequired();
        builder.Property(x => x.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(2000);

        builder.HasIndex(x => new { x.PublishedAtUtc, x.Id })
            .HasDatabaseName("ix_audit_outbox_pending");
        builder.HasIndex(x => x.CreatedAtUtc)
            .HasDatabaseName("ix_audit_outbox_created_at_utc");
        builder.HasIndex(x => x.EventId)
            .IsUnique()
            .HasDatabaseName("ux_audit_outbox_event_id");
    }
}

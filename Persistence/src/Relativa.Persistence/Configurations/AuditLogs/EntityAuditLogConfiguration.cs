using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities.AuditLogs;

namespace Relativa.Persistence.Configurations.AuditLogs;

public class EntityAuditLogConfiguration : IEntityTypeConfiguration<EntityAuditLog>
{
    public void Configure(EntityTypeBuilder<EntityAuditLog> builder)
    {
        builder.ToTable("entity_audit_log");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.EntityId).HasColumnName("entity_id");
        builder.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(50);
        builder.Property(e => e.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ChangedById).HasColumnName("changed_by");
        builder.Property(e => e.FieldName).HasColumnName("field_name");
        builder.Property(e => e.OldValue).HasColumnName("old_value").HasColumnType("jsonb");
        builder.Property(e => e.NewValue).HasColumnName("new_value").HasColumnType("jsonb");
        builder.Property(e => e.ChangedAt).HasColumnName("changed_at").HasColumnType("timestamptz").IsRequired();
        builder.HasOne(e => e.Entity)
            .WithMany()
            .HasForeignKey(e => e.EntityId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_entity_audit_log_entities");
        builder.HasOne(e => e.ChangedBy)
            .WithMany()
            .HasForeignKey(e => e.ChangedById)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_entity_audit_log_users");
        builder.HasIndex(e => e.EntityId).HasDatabaseName("ix_entity_audit_log_entity_id");
        builder.HasIndex(e => e.ChangedAt).IsDescending().HasDatabaseName("ix_entity_audit_log_changed_at");
    }
}

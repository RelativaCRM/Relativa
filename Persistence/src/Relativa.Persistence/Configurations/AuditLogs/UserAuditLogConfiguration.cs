using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities.AuditLogs;

namespace Relativa.Persistence.Configurations.AuditLogs;

public class UserAuditLogConfiguration : IEntityTypeConfiguration<UserAuditLog>
{
    public void Configure(EntityTypeBuilder<UserAuditLog> builder)
    {
        builder.ToTable("user_audit_log");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.TargetUserId).HasColumnName("target_user_id");
        builder.Property(e => e.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ChangedById).HasColumnName("changed_by");
        builder.Property(e => e.FieldName).HasColumnName("field_name");
        builder.Property(e => e.OldValue).HasColumnName("old_value").HasColumnType("jsonb");
        builder.Property(e => e.NewValue).HasColumnName("new_value").HasColumnType("jsonb");
        builder.Property(e => e.ChangedAt).HasColumnName("changed_at").HasColumnType("timestamptz").IsRequired();
        builder.HasOne(e => e.TargetUser)
            .WithMany()
            .HasForeignKey(e => e.TargetUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_user_audit_log_target_users");
        builder.HasOne(e => e.ChangedBy)
            .WithMany()
            .HasForeignKey(e => e.ChangedById)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_user_audit_log_users");
        builder.HasIndex(e => e.TargetUserId).HasDatabaseName("ix_user_audit_log_target_user_id");
        builder.HasIndex(e => e.ChangedAt).IsDescending().HasDatabaseName("ix_user_audit_log_changed_at");
    }
}

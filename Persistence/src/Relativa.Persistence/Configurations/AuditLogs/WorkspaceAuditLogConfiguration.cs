using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities.AuditLogs;

namespace Relativa.Persistence.Configurations.AuditLogs;

public class WorkspaceAuditLogConfiguration : IEntityTypeConfiguration<WorkspaceAuditLog>
{
    public void Configure(EntityTypeBuilder<WorkspaceAuditLog> builder)
    {
        builder.ToTable("workspace_audit_log");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(e => e.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ChangedById).HasColumnName("changed_by");
        builder.Property(e => e.FieldName).HasColumnName("field_name");
        builder.Property(e => e.OldValue).HasColumnName("old_value").HasColumnType("jsonb");
        builder.Property(e => e.NewValue).HasColumnName("new_value").HasColumnType("jsonb");
        builder.Property(e => e.ChangedAt).HasColumnName("changed_at").HasColumnType("timestamptz").IsRequired();
        builder.HasOne(e => e.Workspace)
            .WithMany()
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_workspace_audit_log_workspaces");
        builder.HasOne(e => e.ChangedBy)
            .WithMany()
            .HasForeignKey(e => e.ChangedById)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_workspace_audit_log_users");
        builder.HasIndex(e => e.WorkspaceId).HasDatabaseName("ix_workspace_audit_log_workspace_id");
        builder.HasIndex(e => e.ChangedAt).IsDescending().HasDatabaseName("ix_workspace_audit_log_changed_at");
    }
}

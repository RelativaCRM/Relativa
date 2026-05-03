using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities.AuditLogs;

namespace Relativa.Persistence.Configurations.AuditLogs;

public class OrganizationAuditLogConfiguration : IEntityTypeConfiguration<OrganizationAuditLog>
{
    public void Configure(EntityTypeBuilder<OrganizationAuditLog> builder)
    {
        builder.ToTable("organization_audit_log");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id");
        builder.Property(e => e.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ChangedById).HasColumnName("changed_by");
        builder.Property(e => e.FieldName).HasColumnName("field_name");
        builder.Property(e => e.OldValue).HasColumnName("old_value").HasColumnType("jsonb");
        builder.Property(e => e.NewValue).HasColumnName("new_value").HasColumnType("jsonb");
        builder.Property(e => e.ChangedAt).HasColumnName("changed_at").HasColumnType("timestamptz").IsRequired();
        builder.HasOne(e => e.Organization)
            .WithMany()
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_organization_audit_log_organizations");
        builder.HasOne(e => e.ChangedBy)
            .WithMany()
            .HasForeignKey(e => e.ChangedById)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_organization_audit_log_users");
        builder.HasIndex(e => e.ChangedAt).IsDescending().HasDatabaseName("ix_organization_audit_log_changed_at");
        builder.HasIndex(e => new { e.OrganizationId, e.ChangedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_oal_organization_changed_at");
        builder.HasIndex(e => new { e.ChangedById, e.ChangedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_oal_changed_by_changed_at");
    }
}

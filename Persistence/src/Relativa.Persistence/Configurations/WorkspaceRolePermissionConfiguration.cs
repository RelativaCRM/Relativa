using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class WorkspaceRolePermissionConfiguration : IEntityTypeConfiguration<WorkspaceRolePermission>
{
    public void Configure(EntityTypeBuilder<WorkspaceRolePermission> builder)
    {
        builder.ToTable("workspace_role_permissions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.WsRoleId).HasColumnName("ws_role_id").IsRequired();
        builder.Property(e => e.PermissionId).HasColumnName("permission_id").IsRequired();
        builder.HasOne(e => e.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(e => e.WsRoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_wsrp_role");
        builder.HasOne(e => e.Permission)
            .WithMany(p => p.WorkspaceRolePermissions)
            .HasForeignKey(e => e.PermissionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_wsrp_permission");
    }
}

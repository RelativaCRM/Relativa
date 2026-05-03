using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class UserRoleWorkspaceConfiguration : IEntityTypeConfiguration<UserRoleWorkspace>
{
    public void Configure(EntityTypeBuilder<UserRoleWorkspace> builder)
    {
        builder.ToTable("user_role_workspace");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(e => e.WsRoleId).HasColumnName("ws_role_id").IsRequired();
        builder.Property(e => e.JoinedAt).HasColumnName("joined_at").IsRequired();
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
        builder.HasIndex(e => new { e.UserId, e.WorkspaceId })
            .IsUnique()
            .HasDatabaseName("ix_user_role_workspace_user_ws");
        builder.HasIndex(e => new { e.WorkspaceId, e.IsArchived })
            .HasDatabaseName("ix_urw_workspace_active");
        builder.HasOne(e => e.User)
            .WithMany(u => u.WorkspaceMemberships)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_urw_user");
        builder.HasOne(e => e.Workspace)
            .WithMany(w => w.Members)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_urw_workspace");
        builder.HasOne(e => e.Role)
            .WithMany(r => r.WorkspaceMembers)
            .HasForeignKey(e => e.WsRoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_urw_role");
    }
}

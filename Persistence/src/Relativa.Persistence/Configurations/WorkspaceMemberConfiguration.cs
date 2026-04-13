using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("workspace_members");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(e => e.RoleId).HasColumnName("role_id").IsRequired();
        builder.Property(e => e.JoinedAt).HasColumnName("joined_at").IsRequired();
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
        builder.HasIndex(e => new { e.UserId, e.WorkspaceId })
            .IsUnique()
            .HasDatabaseName("ix_workspace_members_user_workspace");
        builder.HasOne(e => e.User)
            .WithMany(u => u.WorkspaceMembers)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_wm_user");
        builder.HasOne(e => e.Workspace)
            .WithMany(w => w.Members)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_wm_workspace");
        builder.HasOne(e => e.Role)
            .WithMany(r => r.WorkspaceMembers)
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_wm_role");
    }
}

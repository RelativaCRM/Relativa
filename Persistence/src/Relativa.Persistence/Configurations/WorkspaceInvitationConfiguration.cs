using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.ToTable("workspace_invitations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(e => e.Email).HasColumnName("email").IsRequired();
        builder.Property(e => e.WsRoleId).HasColumnName("ws_role_id").IsRequired();
        builder.Property(e => e.InvitedByUserId).HasColumnName("invited_by_user_id").IsRequired();
        builder.Property(e => e.Token).HasColumnName("token").IsRequired();
        builder.HasIndex(e => e.Token)
            .IsUnique()
            .HasDatabaseName("ix_workspace_invitations_token");
        builder.HasIndex(e => new { e.WorkspaceId, e.Status })
            .HasDatabaseName("ix_wi_workspace_status");
        builder.HasIndex(e => new { e.Email, e.Status })
            .HasDatabaseName("ix_wi_email_status");
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.HasOne(e => e.Workspace)
            .WithMany(w => w.Invitations)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_wi_workspace");
        builder.HasOne(e => e.Role)
            .WithMany()
            .HasForeignKey(e => e.WsRoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_wi_role");
        builder.HasOne(e => e.InvitedBy)
            .WithMany()
            .HasForeignKey(e => e.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_wi_invited_by");
    }
}

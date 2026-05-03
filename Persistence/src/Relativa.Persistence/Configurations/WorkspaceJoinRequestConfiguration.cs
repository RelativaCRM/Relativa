using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class WorkspaceJoinRequestConfiguration : IEntityTypeConfiguration<WorkspaceJoinRequest>
{
    public void Configure(EntityTypeBuilder<WorkspaceJoinRequest> builder)
    {
        builder.ToTable("workspace_join_requests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(e => e.Message).HasColumnName("message");
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_wjr_user");
        builder.HasOne(e => e.Workspace)
            .WithMany(w => w.JoinRequests)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_wjr_workspace");
        builder.HasOne(e => e.ReviewedBy)
            .WithMany()
            .HasForeignKey(e => e.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_wjr_reviewed_by");
        builder.HasIndex(e => new { e.WorkspaceId, e.Status })
            .HasDatabaseName("ix_wjr_workspace_status");
        builder.HasIndex(e => new { e.UserId, e.Status })
            .HasDatabaseName("ix_wjr_user_status");
    }
}

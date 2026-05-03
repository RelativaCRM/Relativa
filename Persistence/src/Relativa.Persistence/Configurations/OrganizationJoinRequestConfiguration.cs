using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class OrganizationJoinRequestConfiguration : IEntityTypeConfiguration<OrganizationJoinRequest>
{
    public void Configure(EntityTypeBuilder<OrganizationJoinRequest> builder)
    {
        builder.ToTable("organization_join_requests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(e => e.Message).HasColumnName("message");
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ojr_user");
        builder.HasOne(e => e.Organization)
            .WithMany(o => o.JoinRequests)
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ojr_organization");
        builder.HasOne(e => e.ReviewedBy)
            .WithMany()
            .HasForeignKey(e => e.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_ojr_reviewed_by");
        builder.HasIndex(e => new { e.OrganizationId, e.Status })
            .HasDatabaseName("ix_ojr_org_status");
        builder.HasIndex(e => new { e.UserId, e.Status })
            .HasDatabaseName("ix_ojr_user_status");
    }
}

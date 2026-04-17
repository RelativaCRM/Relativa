using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class OrganizationInvitationConfiguration : IEntityTypeConfiguration<OrganizationInvitation>
{
    public void Configure(EntityTypeBuilder<OrganizationInvitation> builder)
    {
        builder.ToTable("organization_invitations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(e => e.Email).HasColumnName("email").IsRequired();
        builder.Property(e => e.InvitedByUserId).HasColumnName("invited_by_user_id").IsRequired();
        builder.Property(e => e.Token).HasColumnName("token").IsRequired();
        builder.HasIndex(e => e.Token)
            .IsUnique()
            .HasDatabaseName("ix_org_invitations_token");
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.HasOne(e => e.Organization)
            .WithMany(o => o.Invitations)
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_oi_organization");
        builder.HasOne(e => e.InvitedBy)
            .WithMany()
            .HasForeignKey(e => e.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_oi_invited_by");
    }
}

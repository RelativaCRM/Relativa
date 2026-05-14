using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class UserRoleOrganizationConfiguration : IEntityTypeConfiguration<UserRoleOrganization>
{
    public void Configure(EntityTypeBuilder<UserRoleOrganization> builder)
    {
        builder.ToTable("user_role_organization");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(e => e.OrgRoleId).HasColumnName("org_role_id").IsRequired();
        builder.Property(e => e.JoinedAt).HasColumnName("joined_at").IsRequired();
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
        builder.HasIndex(e => new { e.UserId, e.OrganizationId })
            .IsUnique()
            .HasDatabaseName("ix_user_role_org_user_org");
        builder.HasIndex(e => new { e.OrganizationId, e.IsArchived })
            .HasDatabaseName("ix_uro_org_active");
        builder.HasOne(e => e.User)
            .WithMany(u => u.OrganizationMemberships)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_uro_user");
        builder.HasOne(e => e.Organization)
            .WithMany(o => o.Members)
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_uro_organization");
        builder.HasOne(e => e.Role)
            .WithMany(r => r.OrganizationMembers)
            .HasForeignKey(e => e.OrgRoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_uro_role");
    }
}

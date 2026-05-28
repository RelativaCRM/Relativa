using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class OrganizationSettingsConfiguration : IEntityTypeConfiguration<OrganizationSettings>
{
    public void Configure(EntityTypeBuilder<OrganizationSettings> builder)
    {
        builder.ToTable("organization_settings", t =>
        {
            t.HasCheckConstraint("ck_org_settings_join_policy", "join_policy IN ('open', 'invite_only')");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired(false);
        builder.Property(e => e.JoinPolicy)
            .HasColumnName("join_policy")
            .HasMaxLength(12)
            .HasDefaultValue("open")
            .IsRequired();
        builder.Property(e => e.DefaultOrgRoleId)
            .HasColumnName("default_org_role_id")
            .IsRequired(false);
        builder.HasIndex(e => e.OrganizationId)
            .IsUnique()
            .HasDatabaseName("ix_org_settings_organization_id");
        builder.HasOne(e => e.Organization)
            .WithOne(o => o.Settings)
            .HasForeignKey<OrganizationSettings>(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_org_settings_organization");
        builder.HasOne(e => e.DefaultOrgRole)
            .WithMany()
            .HasForeignKey(e => e.DefaultOrgRoleId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_org_settings_default_org_role");
    }
}

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
            t.HasCheckConstraint("ck_org_settings_high_risk", "high_risk_threshold BETWEEN 0.00 AND 1.00");
            t.HasCheckConstraint("ck_org_settings_medium_risk", "medium_risk_threshold BETWEEN 0.00 AND 1.00 AND medium_risk_threshold < high_risk_threshold");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(e => e.HighRiskThreshold)
            .HasColumnName("high_risk_threshold")
            .HasColumnType("numeric(3,2)")
            .HasDefaultValue(0.7m)
            .ValueGeneratedNever()
            .IsRequired();
        builder.Property(e => e.MediumRiskThreshold)
            .HasColumnName("medium_risk_threshold")
            .HasColumnType("numeric(3,2)")
            .HasDefaultValue(0.4m)
            .ValueGeneratedNever()
            .IsRequired();
        builder.HasIndex(e => e.WorkspaceId)
            .IsUnique()
            .HasDatabaseName("ix_organization_settings_workspace_id");
        builder.HasOne(e => e.Workspace)
            .WithOne(w => w.OrganizationSettings)
            .HasForeignKey<OrganizationSettings>(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_org_settings_workspace");
    }
}

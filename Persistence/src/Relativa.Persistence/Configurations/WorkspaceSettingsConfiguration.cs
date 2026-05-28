using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class WorkspaceSettingsConfiguration : IEntityTypeConfiguration<WorkspaceSettings>
{
    public void Configure(EntityTypeBuilder<WorkspaceSettings> builder)
    {
        builder.ToTable("workspace_settings", t =>
        {
            t.HasCheckConstraint("ck_workspace_settings_high_risk", "high_risk_threshold BETWEEN 0.00 AND 1.00");
            t.HasCheckConstraint("ck_workspace_settings_medium_risk", "medium_risk_threshold BETWEEN 0.00 AND 1.00 AND medium_risk_threshold < high_risk_threshold");
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
        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired(false);
        builder.Property(e => e.RiskScoringEnabled)
            .HasColumnName("risk_scoring_enabled")
            .HasDefaultValue(true)
            .IsRequired();
        builder.HasIndex(e => e.WorkspaceId)
            .IsUnique()
            .HasDatabaseName("ix_workspace_settings_workspace_id");
        builder.HasOne(e => e.Workspace)
            .WithOne(w => w.WorkspaceSettings)
            .HasForeignKey<WorkspaceSettings>(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_workspace_settings_workspace");
    }
}

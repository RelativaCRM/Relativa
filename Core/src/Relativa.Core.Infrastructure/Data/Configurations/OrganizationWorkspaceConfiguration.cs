using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Core.Domain.Entities;

namespace Relativa.Core.Infrastructure.Data.Configurations;

public class OrganizationWorkspaceConfiguration : IEntityTypeConfiguration<OrganizationWorkspace>
{
    public void Configure(EntityTypeBuilder<OrganizationWorkspace> builder)
    {
        builder.ToTable("organization_workspaces");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.OrgId).HasColumnName("org_id").IsRequired();
        builder.Property(e => e.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.HasOne(e => e.Organization)
            .WithMany(o => o.OrganizationWorkspaces)
            .HasForeignKey(e => e.OrgId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ow_org");
        builder.HasOne(e => e.Workspace)
            .WithMany(w => w.OrganizationWorkspaces)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ow_workspace");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class WorkspaceRoleConfiguration : IEntityTypeConfiguration<WorkspaceRole>
{
    public void Configure(EntityTypeBuilder<WorkspaceRole> builder)
    {
        builder.ToTable("workspace_roles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired();
        builder.Property(e => e.WorkspaceId).HasColumnName("workspace_id").IsRequired(false);
        builder.HasIndex(e => new { e.Name, e.WorkspaceId })
            .IsUnique()
            .HasDatabaseName("ix_workspace_roles_name_workspace");
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
        builder.HasOne(e => e.Workspace)
            .WithMany(w => w.Roles)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ws_roles_workspace");
    }
}

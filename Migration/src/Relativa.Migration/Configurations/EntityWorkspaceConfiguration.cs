using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Migration.Models;
namespace Relativa.Migration.Configurations;
public class EntityWorkspaceConfiguration : IEntityTypeConfiguration<EntityWorkspace> {
    public void Configure(EntityTypeBuilder<EntityWorkspace> builder) {
        builder.ToTable("entity_workspaces");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(e => e.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.HasOne(e => e.Entity).WithMany(e => e.EntityWorkspaces).HasForeignKey(e => e.EntityId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_ew_entity");
        builder.HasOne(e => e.Workspace).WithMany(w => w.EntityWorkspaces).HasForeignKey(e => e.WorkspaceId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_ew_workspace");
    }
}

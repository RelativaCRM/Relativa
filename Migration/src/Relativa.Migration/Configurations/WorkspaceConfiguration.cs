using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Migration.Models;
namespace Relativa.Migration.Configurations;
public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace> {
    public void Configure(EntityTypeBuilder<Workspace> builder) {
        builder.ToTable("workspaces");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired();
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
    }
}

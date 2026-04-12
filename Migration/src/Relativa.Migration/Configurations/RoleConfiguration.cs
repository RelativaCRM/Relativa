using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Migration.Models;
namespace Relativa.Migration.Configurations;
public class RoleConfiguration : IEntityTypeConfiguration<Role> {
    public void Configure(EntityTypeBuilder<Role> builder) {
        builder.ToTable("roles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired();
        builder.HasIndex(e => e.Name).IsUnique();
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
    }
}

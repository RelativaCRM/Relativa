using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Migration.Models;
namespace Relativa.Migration.Configurations;
public class EntityTypeConfiguration : IEntityTypeConfiguration<EntityType> {
    public void Configure(EntityTypeBuilder<EntityType> builder) {
        builder.ToTable("entity_types");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.TypeId).HasColumnName("type_id").IsRequired();
        builder.HasIndex(e => e.TypeId).IsUnique();
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
    }
}

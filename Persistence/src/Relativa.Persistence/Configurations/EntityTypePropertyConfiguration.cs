using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class EntityTypePropertyConfiguration : IEntityTypeConfiguration<EntityTypeProperty>
{
    public void Configure(EntityTypeBuilder<EntityTypeProperty> builder)
    {
        builder.ToTable("entity_type_property");
        builder.HasKey(e => new { e.EntityTypeId, e.PropertyId });
        builder.Property(e => e.EntityTypeId).HasColumnName("entity_type_id").IsRequired();
        builder.Property(e => e.PropertyId).HasColumnName("property_id").IsRequired();
        builder.Property(e => e.IsRequired).HasColumnName("is_required").HasDefaultValue(false);
        builder.HasOne(e => e.EntityType)
            .WithMany(t => t.EntityTypeProperties)
            .HasForeignKey(e => e.EntityTypeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_etp_entity_type");
        builder.HasOne(e => e.Property)
            .WithMany(p => p.EntityTypeProperties)
            .HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_etp_property");
    }
}

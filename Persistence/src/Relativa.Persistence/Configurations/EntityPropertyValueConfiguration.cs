using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class EntityPropertyValueConfiguration : IEntityTypeConfiguration<EntityPropertyValue>
{
    public void Configure(EntityTypeBuilder<EntityPropertyValue> builder)
    {
        builder.ToTable("entity_property_value");
        builder.HasKey(e => new { e.EntityId, e.PropertyId });
        builder.Property(e => e.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(e => e.PropertyId).HasColumnName("property_id").IsRequired();
        builder.Property(e => e.ValueString).HasColumnName("value_string");
        builder.Property(e => e.ValueInt).HasColumnName("value_int");
        builder.Property(e => e.ValueDecimal).HasColumnName("value_decimal");
        builder.Property(e => e.ValueBool).HasColumnName("value_bool");
        builder.Property(e => e.ValueDate).HasColumnName("value_date");
        builder.HasIndex(e => e.EntityId).HasDatabaseName("ix_epv_entity_id");
        builder.HasIndex(e => e.PropertyId).HasDatabaseName("ix_epv_property_id");
        builder.HasOne(e => e.Entity)
            .WithMany(en => en.EntityPropertyValues)
            .HasForeignKey(e => e.EntityId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_epv_entity");
        builder.HasOne(e => e.Property)
            .WithMany(p => p.EntityPropertyValues)
            .HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_epv_property");
    }
}

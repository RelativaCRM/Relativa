using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class EntityRelationshipTypeConfiguration : IEntityTypeConfiguration<EntityRelationshipType>
{
    public void Configure(EntityTypeBuilder<EntityRelationshipType> builder)
    {
        builder.ToTable("entity_relationship_type");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired();
        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("ix_entity_relationship_type_name");
        builder.Property(e => e.SourceEntityTypeId).HasColumnName("source_entity_type_id").IsRequired();
        builder.Property(e => e.TargetEntityTypeId).HasColumnName("target_entity_type_id").IsRequired();
        builder.Property(e => e.IsRequired).HasColumnName("is_required").IsRequired();
        builder.Property(e => e.RelationshipCardinality)
            .HasColumnName("relationship_cardinality")
            .HasMaxLength(32)
            .IsRequired()
            .HasConversion(
                v => v.ToDatabaseValue(),
                v => RelationshipCardinalityExtensions.ParseDatabaseValue(v));
        builder.HasOne(e => e.SourceEntityType)
            .WithMany(t => t.SourceRelationshipTypes)
            .HasForeignKey(e => e.SourceEntityTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ert_source_entity_type");
        builder.HasOne(e => e.TargetEntityType)
            .WithMany(t => t.TargetRelationshipTypes)
            .HasForeignKey(e => e.TargetEntityTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ert_target_entity_type");
    }
}

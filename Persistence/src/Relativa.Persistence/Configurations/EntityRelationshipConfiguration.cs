using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class EntityRelationshipConfiguration : IEntityTypeConfiguration<EntityRelationship>
{
    public void Configure(EntityTypeBuilder<EntityRelationship> builder)
    {
        builder.ToTable("entity_relationship");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.SourceEntityId).HasColumnName("source_entity_id").IsRequired();
        builder.Property(e => e.TargetEntityId).HasColumnName("target_entity_id").IsRequired();
        builder.Property(e => e.RelationshipTypeId).HasColumnName("relationship_type_id").IsRequired();
        builder.HasIndex(e => e.SourceEntityId).HasDatabaseName("ix_er_source_entity_id");
        builder.HasIndex(e => e.RelationshipTypeId).HasDatabaseName("ix_er_relationship_type_id");
        builder.HasOne(e => e.SourceEntity)
            .WithMany(en => en.SourceRelationships)
            .HasForeignKey(e => e.SourceEntityId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_er_source_entity");
        builder.HasOne(e => e.TargetEntity)
            .WithMany(en => en.TargetRelationships)
            .HasForeignKey(e => e.TargetEntityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_er_target_entity");
        builder.HasOne(e => e.RelationshipType)
            .WithMany(rt => rt.EntityRelationships)
            .HasForeignKey(e => e.RelationshipTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_er_relationship_type");
    }
}

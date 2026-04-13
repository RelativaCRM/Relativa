using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class EntityConfiguration : IEntityTypeConfiguration<Entity>
{
    public void Configure(EntityTypeBuilder<Entity> builder)
    {
        builder.ToTable("entities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Type).HasColumnName("type").IsRequired();
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
        builder.HasOne(e => e.EntityType)
            .WithMany(t => t.Entities)
            .HasForeignKey(e => e.Type)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_entities_type");
    }
}

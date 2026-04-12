using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Core.Domain.Entities;
using DomainEntityType = Relativa.Core.Domain.Entities.EntityType;

namespace Relativa.Core.Infrastructure.Data.Configurations;

public class EntityTypeConfiguration : IEntityTypeConfiguration<DomainEntityType>
{
    public void Configure(EntityTypeBuilder<DomainEntityType> builder)
    {
        builder.ToTable("entity_types");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.TypeId).HasColumnName("type_id").IsRequired();
        builder.HasIndex(e => e.TypeId).IsUnique();
        builder.Property(e => e.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
    }
}

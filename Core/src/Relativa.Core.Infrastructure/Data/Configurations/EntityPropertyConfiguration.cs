using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Core.Domain.Entities;

namespace Relativa.Core.Infrastructure.Data.Configurations;

public class EntityPropertyConfiguration : IEntityTypeConfiguration<EntityProperty>
{
    public void Configure(EntityTypeBuilder<EntityProperty> builder)
    {
        builder.ToTable("entity_properties");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.EntityId).HasColumnName("entity_id").IsRequired();
        builder.HasIndex(e => e.EntityId).IsUnique();
        builder.Property(e => e.PersonalDataPropertyId).HasColumnName("personal_data_property_id");
        builder.Property(e => e.LocationPropertyId).HasColumnName("location_property_id");
        builder.Property(e => e.DealPropertyId).HasColumnName("deal_property_id");
        builder.HasOne(e => e.Entity)
            .WithOne(e => e.EntityProperty)
            .HasForeignKey<EntityProperty>(e => e.EntityId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ep_entity");
        builder.HasOne(e => e.PersonalDataProperty)
            .WithMany()
            .HasForeignKey(e => e.PersonalDataPropertyId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_ep_personal");
        builder.HasOne(e => e.LocationProperty)
            .WithMany()
            .HasForeignKey(e => e.LocationPropertyId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_ep_location");
        builder.HasOne(e => e.DealProperty)
            .WithMany()
            .HasForeignKey(e => e.DealPropertyId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_ep_deal");
    }
}

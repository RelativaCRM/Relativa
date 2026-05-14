using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class PropertyAllowedValueConfiguration : IEntityTypeConfiguration<PropertyAllowedValue>
{
    public void Configure(EntityTypeBuilder<PropertyAllowedValue> builder)
    {
        builder.ToTable("property_allowed_value");
        builder.HasKey(pav => new { pav.PropertyId, pav.Value });
        builder.Property(pav => pav.PropertyId).HasColumnName("property_id");
        builder.Property(pav => pav.Value).HasColumnName("value").IsRequired();
        builder.HasOne(pav => pav.Property)
            .WithMany(p => p.AllowedValues)
            .HasForeignKey(pav => pav.PropertyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_property_allowed_value_property");
    }
}

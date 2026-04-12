using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class LocationPropertyValueConfiguration : IEntityTypeConfiguration<LocationPropertyValue>
{
    public void Configure(EntityTypeBuilder<LocationPropertyValue> builder)
    {
        builder.ToTable("location_property_values");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.Country).HasColumnName("country");
        builder.Property(e => e.Region).HasColumnName("region");
        builder.Property(e => e.State).HasColumnName("state");
        builder.Property(e => e.City).HasColumnName("city");
        builder.Property(e => e.Address).HasColumnName("address");
        builder.Property(e => e.PostalCode).HasColumnName("postal_code");
        builder.Property(e => e.Locale).HasColumnName("locale");
        builder.Property(e => e.Timezone).HasColumnName("timezone");
    }
}

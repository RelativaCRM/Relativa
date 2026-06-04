using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Relativa.Persistence.Entities;

namespace Relativa.Persistence.Configurations;

public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("property");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(p => p.Name).HasColumnName("name").IsRequired();
        builder.Property(p => p.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        builder.Property(p => p.DataType)
            .HasColumnName("data_type")
            .HasConversion<string>()
            .IsRequired();
        builder.Property(p => p.IsReadonly).HasColumnName("is_readonly").IsRequired();
        builder.Property(p => p.OrganizationId).HasColumnName("organization_id");
        builder.HasOne(p => p.Organization)
            .WithMany()
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_property_organization");
    }
}
